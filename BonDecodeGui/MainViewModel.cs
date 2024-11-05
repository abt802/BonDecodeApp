using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging.Messages;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using CommunityToolkit.Mvvm.Messaging;

namespace BonDecodeGui
{

    internal partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(DecodeCommand))]
        private string _decodeDll = "B61Decoder.dll";

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(DecodeCommand))]
        private string _targetFiles = "";

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(DecodeCommand))]
        private string _destinationFolder = ".";

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(DecodeCommand))]
        private bool _appendSuffix = true;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(DecodeCommand))]
        private string _suffix = "_dec";

        [ObservableProperty]
        private bool _hideShell = true;

        [ObservableProperty]
        private string _currentStatus = string.Empty;

        [ObservableProperty]
        private ObservableCollection<string> _dllCollection = new ObservableCollection<string>();

        public IConfiguration Config { get; private set; }


        public MainViewModel()
        {
            GlobDlls();

            Config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();
            var settings = Config.Get<Settings>();
            if (settings == null)
            {
                WeakReferenceMessenger.Default.Send<ProcessMessage>(new("Not found appsettings.json", false));
                return;
            }
            _decodeDll = settings.DecodeDll;
            _destinationFolder = settings.DestinationFolder;
            _appendSuffix = settings.AppendSuffix;
            _suffix = settings.Suffix;
            _hideShell = settings.HideShell;

        }

        private void GlobDlls()
        {
            Matcher matcher = new();
            matcher.AddInclude("*.dll");
            matcher.AddExcludePatterns(new[] 
            {
                //for Release
                "BonDecodeGui.Dll", "Microsoft.*", "CommunityToolkit.*" ,
                //for Single File App(Publish)
                "*cor3.dll"
            });
            var result = matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory)));
            foreach (var matchingFile in result.Files)
            {
                DllCollection.Add(matchingFile.Path);
            }
        }

        private bool CanDecode()
        {
            if (string.IsNullOrEmpty(DecodeDll)) return false;
            if (string.IsNullOrEmpty(TargetFiles)) return false;
            if (string.IsNullOrEmpty(DestinationFolder)) return false;
            if (AppendSuffix)
            {
                if (string.IsNullOrEmpty(Suffix)) return false;
            }
            return true;
        }

        [RelayCommand(CanExecute =nameof(CanDecode), IncludeCancelCommand =true)]
        private async Task DecodeAsync(CancellationToken token)
        {
            try
            {
                bool aborted = false;
                var files = TargetFiles.Split(Environment.NewLine);
                foreach (var target in files)
                {
                    if (token.IsCancellationRequested) { break; }

                    CurrentStatus = target;

                    if (!File.Exists(target)) continue;

                    var targetWrapped = $"\"{target}\"";
                    var destFilename = Path.GetFileName(target);
                    if (AppendSuffix)
                    {
                        var name = Path.GetFileNameWithoutExtension(destFilename);
                        var ext = Path.GetExtension(destFilename);
                        destFilename = $"{name}{Suffix}{ext}";
                    }
                    var destWrapped = "\"" +  Path.Combine(DestinationFolder, destFilename) + "\"";
                    using (Process myProc = new()
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                            FileName = "BonDecodeApp.exe",
                            Arguments = $"{DecodeDll} {targetWrapped} {destWrapped}",
                            RedirectStandardOutput = HideShell,
                            RedirectStandardError = HideShell,
                            CreateNoWindow = HideShell,
                            UseShellExecute = false,
                            ErrorDialog = true,
                        }
                    })
                    {
                        if (HideShell)
                        {
                            myProc.OutputDataReceived += (s, e) =>
                            {
                                if (!string.IsNullOrEmpty(e.Data))
                                {
                                    CurrentStatus = $"{target}: {e.Data}";
                                }
                            };
                            myProc.ErrorDataReceived += (s, e) =>
                            {
                                if (!string.IsNullOrEmpty(e.Data))
                                {
                                    WeakReferenceMessenger.Default.Send<ProcessMessage>(new($"{target}: {e.Data}", false));
                                }
                            };
                        }
                        //Debug.WriteLine($"BonDecodeApp.exe {target} {dest}");
                        myProc.Start();
                        if (HideShell)
                        {
                            myProc.BeginOutputReadLine();
                            myProc.BeginErrorReadLine();
                        }
                        //await myProc.WaitForExitAsync();
                        await MonitorProcessAsync(myProc, token);
                        if (myProc.ExitCode != 0)
                        {
                            aborted = true;
                            break;
                        }
                    }
                }
                var message = (token.IsCancellationRequested || aborted) ? "Decode Aborted" : "Decode Finish";
                WeakReferenceMessenger.Default.Send<ProcessMessage>(new(message));
            }
            catch(OperationCanceledException)
            {
                WeakReferenceMessenger.Default.Send<ProcessMessage>(new("Decode Aborted", false));
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
                WeakReferenceMessenger.Default.Send<ProcessMessage>(new (ex.ToString(), false));
            }
        }

        private async Task MonitorProcessAsync(Process proc, CancellationToken token)
        {
            while (!proc.HasExited)
            {
                await Task.Delay(100);
                if (token.IsCancellationRequested)
                {
                    proc.Kill();
                    throw new OperationCanceledException();
                }
            }
        }

        [RelayCommand]
        private void SaveSettings()
        {
            var settings = Config.Get<Settings>()!;
            settings.DecodeDll = DecodeDll;
            settings.DestinationFolder = DestinationFolder;
            settings.AppendSuffix = AppendSuffix;  
            settings.Suffix = Suffix;  
            settings.HideShell = HideShell;

            JsonSerializerOptions options = new() { WriteIndented = true };

            options.Converters.Add(new JsonStringEnumConverter());
            var newJson = System.Text.Json.JsonSerializer.Serialize(settings, options);
            var appSettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            File.WriteAllText(appSettingsPath, newJson);
        }
    }

    internal class ProcessMessage : ValueChangedMessage<string>
    {
        public bool IsSuccess { get; private set; }
        public ProcessMessage(string value, bool isSuccess = true) : base(value)
        {
            IsSuccess = isSuccess;
        }
    }

}
