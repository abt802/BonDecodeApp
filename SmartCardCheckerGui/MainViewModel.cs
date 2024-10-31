using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Accessibility;
using CommunityToolkit.Mvvm;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace SmartCardCheckerGui
{
    [INotifyPropertyChanged]
    internal partial class MainViewModel
    {
        [ObservableProperty]
        private ObservableCollection<string> _readers = new ObservableCollection<string>();

        [ObservableProperty]
        private string _output = string.Empty;

        [ObservableProperty]
        private bool _felicaCheck = true;

        [ObservableProperty]
        private bool _acasCheck = true;

        [ObservableProperty]
        private bool _bcasCheck = true;

        [ObservableProperty]
        private bool _ignoreWindowsHello = true;

        private SmartCardChecker scc = new();



        [RelayCommand]
        public async Task CheckAsync()
        {
            var res = await Task.Run(scc.ConnectSCS);
            Output = res.sb.ToString();
            if (res.result == false)
            {
                return;
            }
            Output += Environment.NewLine;

            res = await Task.Run(() => scc.CardReaders(IgnoreWindowsHello));
            Output += res.sb.ToString();
            if (res.result == false)
            {
                return;
            }
            Output += Environment.NewLine;

            res = await Task.Run(scc.GetStatusChange);
            Output += res.sb.ToString();
            if (res.result == false)
            {
                return;
            }
            Output += "----" + Environment.NewLine;

            (TransmitType transmit, bool check)[] transmitChecks = 
            [
                (TransmitType.Felica, FelicaCheck),
                (TransmitType.ACAS, AcasCheck),
                (TransmitType.BCAS, BcasCheck),
            ];

            scc.sb.Indent += 2;
            foreach (var reader in scc.CardContextDict.Keys)
            {
                Output += $"{reader}:" + Environment.NewLine;
                res = await Task.Run(() => scc.Connect(reader));
                Output += res.sb.ToString();
                if (res.result == false)
                {
                    Output += "----" + Environment.NewLine;
                    continue;
                }

                res = await Task.Run(() => scc.CardStatus(reader));
                Output += res.sb.ToString();
                if (res.result == false)
                {
                    res = await Task.Run(() => scc.Disconnect(reader));
                    Output += "----" + Environment.NewLine;
                    continue;
                }

                foreach (var tc in transmitChecks)
                {
                    if (tc.check)
                    {
                        res = await Task.Run(() => scc.Transmit(reader, tc.transmit));
                        Output += res.sb.ToString();
                        //if (res.result == false)
                        //{
                        //    Output += "--" + Environment.NewLine;
                        //    break;
                        //}
                    }
                }

                res = await Task.Run(() => scc.Disconnect(reader));
                Output += res.sb.ToString();

                Output += "----" + Environment.NewLine;
            }
            scc.sb.Indent -= 2;

            res = scc.ReleaseSCS();
            Output += res.sb.ToString();
        }




    }
}
