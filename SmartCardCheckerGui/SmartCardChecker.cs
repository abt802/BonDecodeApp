using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace SmartCardCheckerGui
{
    internal enum TransmitType
    {
        Felica,
        ACAS,
        BCAS,
    }

    internal class SmartCardChecker
    {
        public Dictionary<string, IntPtr> CardContextDict { get; set; } =  new Dictionary<string, IntPtr>();

        public StringBuilderIndent sb = new();

        private IntPtr hContext = IntPtr.Zero;

        private void Reset()
        {
            CardContextDict = new Dictionary<string, nint>();
            hContext = IntPtr.Zero;
        }

        public (bool result, StringBuilderIndent sb) ConnectSCS()
        {
            Reset();

            sb.Clear();
            sb.AddLog("SCardEstablishContext:");
            sb.Indent += 2;

            var ret = DllApi.SCardEstablishContext(DllConst.SCARD_SCOPE_USER, IntPtr.Zero, IntPtr.Zero, out hContext);
            if (ret != DllConst.SCARD_S_SUCCESS)
            {
                switch (ret)
                {
                    case DllConst.SCARD_E_NO_SERVICE:
                        sb.AddLog("failure(SmartCard no service)");
                        break;
                    default:
                        sb.AddLog($"failure(err code:{GetErrString(ret)})");
                        break;
                }
                sb.Indent -= 2;
                return (false, sb);
            }
            sb.AddLog("connect SmartCard service.");
            sb.Indent -= 2;
            return (true, sb);
        }

        public (bool result, StringBuilderIndent sb) ReleaseSCS()
        {
            sb.Clear();
            sb.AddLog("SCardReleaseContext:");
            sb.Indent += 2;

            var ret = DllApi.SCardReleaseContext(hContext);
            if (ret != DllConst.SCARD_S_SUCCESS)
            {
                sb.AddLog($"failure(err code:{GetErrString(ret)})");
                sb.Indent -= 2;
                return (false, sb);
            }
            sb.AddLog("disconnected SmartCard service.");
            sb.Indent -= 2;
            return (true, sb);
        }

        public (bool result, StringBuilderIndent sb) CardReaders(bool ignoreHello)
        {
            sb.Clear();
            sb.AddLog("SCardListReaders:");
            sb.Indent += 2;

            if (hContext == IntPtr.Zero)
            {
                throw new Exception("hContext must be not null.");
            }
            uint pcchReaders = 0;

            //リーダー名バッファ長を取得
            var ret = DllApi.SCardListReaders(hContext, null, null, ref pcchReaders);
            if (ret != DllConst.SCARD_S_SUCCESS)
            {
                sb.AddLog($"failure(err code:{GetErrString(ret)})");
                sb.Indent -= 2;
                return (false, sb);
            }

            //バッファ長を元に各リーダー名を取得
            var readers = new byte[pcchReaders * 2];
            ret = DllApi.SCardListReaders(hContext, null, readers, ref pcchReaders);
            if (ret != DllConst.SCARD_S_SUCCESS)
            {
                sb.AddLog($"failure(err code:{GetErrString(ret)})");
                sb.Indent -= 2;
                return (false, sb);
            }

            var encoding = new UnicodeEncoding();
            var encodedReaders =  encoding.GetString(readers);

            int offset = 0;
            int nullindex;
            while(true)
            {
                nullindex = encodedReaders.IndexOf((char)0, offset);
                if (nullindex == offset) { break; }
                var readername = encodedReaders.Substring(offset, nullindex - offset);
                if (!(ignoreHello && readername.StartsWith("Windows Hello"))) 
                {
                    sb.AddLog($"{readername}");
                    CardContextDict[readername] = 0;
                }
                offset = nullindex+1;
            };
            sb.Indent -= 2;
            return (true, sb);
        }

        public (bool result, StringBuilderIndent sb)Connect(string readerName)
        {
            sb.Clear();
            sb.AddLog("SCardConnect:");
            sb.Indent += 2;

            var hCard = IntPtr.Zero;
            IntPtr activeProtocol = IntPtr.Zero;
            var ret = DllApi.SCardConnect(
                hContext, 
                readerName, 
                DllConst.SCARD_SHARE_SHARED, 
                DllConst.SCARD_PROTOCOL_T1, 
                ref hCard, 
                ref activeProtocol);
            if (ret != DllConst.SCARD_S_SUCCESS)
            {
                sb.AddLog($"failure(err code:{GetErrString(ret)})");
                sb.Indent -= 2;
                return (false, sb);
            }
            sb.AddLog("connected.");
            CardContextDict[readerName] = hCard;
            sb.Indent -= 2;
            return (true, sb);
        }

        public (bool result, StringBuilderIndent sb) Disconnect(string readerName)
        {
            sb.Clear();
            sb.AddLog("SCardDisconnect:");
            sb.Indent += 2;

            var hCard = CardContextDict[readerName];
            if (hCard == IntPtr.Zero)
            {
                throw new Exception("SCard Context has zero pointer.");
            }
            var ret = DllApi.SCardDisconnect(hCard, DllConst.SCARD_LEAVE_CARD);

            if (ret != DllConst.SCARD_S_SUCCESS)
            {
                sb.AddLog($"failure(err code:{GetErrString(ret)})");
                sb.Indent -= 2;
                return (false, sb);
            }
            sb.AddLog("disconnected.");
            CardContextDict[readerName] = IntPtr.Zero;
            sb.Indent -= 2;
            return (true, sb);
        }

        public (bool result, StringBuilderIndent sb) Transmit(string readerName, TransmitType transmitType)
        {
            sb.Clear();
            sb.AddLog("SCardTransmit(" + Enum.GetName(transmitType) + "):");
            sb.Indent += 2;

            var hCard = CardContextDict[readerName];
            if (hCard == IntPtr.Zero)
            {
                throw new Exception("SCard Context has zero pointer.");
            }

            UInt32 maxRecvDataLen = 256;
            var recvBuffer = new byte[maxRecvDataLen + 2];


            byte[] pbSendBuffer = transmitType switch
            {
                TransmitType.Felica =>
                [
                    0xFF,       // CLA
                    0xCA,       // INS
                    0x00,       // P1
                    0x00,       // P2
                    0x00        // Le
                ],
                TransmitType.ACAS =>
                [
                    0x90,       // CLA
                    0x32,       // INS
                    0x00,       // P1
                    0x01,       // P2
                    0x00,       // Le
                ],
                TransmitType.BCAS =>
                [
                    0x90,       // CLA
                    0x32,       // INS
                    0x00,       // P1
                    0x00,       // P2
                    0x00,       // Le
                ],
                _ => []
            };

            var ioRecv = new DllApi.SCARD_IO_REQUEST();
            ioRecv.cbPciLength = 255;

            int pcbRecvLength = recvBuffer.Length;
            int cbSendLength = pbSendBuffer.Length;

            IntPtr handle = DllApi.LoadLibrary("winscard.dll");
            IntPtr pci = DllApi.GetProcAddress(handle, "g_rgSCardT1Pci");
            DllApi.FreeLibrary(handle);

            var ret = DllApi.SCardTransmit(
                hCard, 
                pci, 
                pbSendBuffer, 
                cbSendLength, 
                ioRecv, 
                recvBuffer, 
                ref pcbRecvLength
                );
            if (ret != DllConst.SCARD_S_SUCCESS)
            {
                sb.AddLog($"failure(err code:{GetErrString(ret)})");
                sb.Indent -= 2;
                return (false, sb);
            }

            var result = BitConverter.ToString(recvBuffer, 0, pcbRecvLength - 2);
            if (recvBuffer[pcbRecvLength-2] == 0x90 && recvBuffer[pcbRecvLength-1] == 0x0)
            {
                sb.AddLog( "success.");
                sb.AddLog($"  recv:{result}");
            }
            else
            {
                sb.AddLog("failure:");
                sb.AddLog($"  sw1:" + BitConverter.ToString(recvBuffer, pcbRecvLength - 2, 1));
                sb.AddLog($"  sw2:" + BitConverter.ToString(recvBuffer, pcbRecvLength - 1, 1));
            }
            sb.Indent -= 2;
            return (true, sb);
        }

        public (bool result, StringBuilderIndent sb) GetStatusChange()
        {
            sb.Clear();
            sb.AddLog("SCardGetStatusChange:");
            sb.Indent += 2;

            if (hContext == IntPtr.Zero)
            {
                throw new Exception("hContext must be not null.");
            }

            if (CardContextDict.Count == 0) 
            {
                throw new Exception("");
            }

            int timeout = 500;

            var readerState = new DllApi.SCARD_READERSTATE[CardContextDict.Count];
            int i = 0;
            foreach (var reader in CardContextDict.Keys)
            {
                readerState[i].dwCurrentState = DllConst.SCARD_STATE_UNAWARE;
                readerState[i].szReader = reader;
                i++;
            }
            var ret = DllApi.SCardGetStatusChange(hContext, timeout, readerState, readerState.Count());

            if (ret != DllConst.SCARD_S_SUCCESS)
            {
                sb.AddLog($"failure(err code:{GetErrString(ret)})");
                sb.Indent -= 2;
                return (false, sb);
            }

            foreach (var state in readerState)
            {
                var statuses = GetCurrentStatus(state.dwCurrentState);
                var events = GetEventStatus(state.dwEventState);
                sb.AddLog($"{state.szReader}:");
                sb.AddLog($"  CurrentState:(0x{state.dwCurrentState:X})");
                foreach (var item in statuses)
                {
                    sb.AddLog("    " + item);
                }
                sb.AddLog($"  EventState:(0x{state.dwEventState:X})");
                foreach (var item in events)
                {
                    sb.AddLog("    " + item);
                }
            }
            sb.Indent -= 2;
            return (true, sb);

        }

        public (bool result, StringBuilderIndent sb) CardStatus(string readerName)
        {
            sb.Clear();
            sb.AddLog("SCardStatus:");
            sb.Indent += 2;

            var hCard = CardContextDict[readerName];
            if (hCard == IntPtr.Zero)
            {
                throw new Exception("SCard Context has zero pointer.");
            }


            Int32 cch = readerName.Length;
            Int32 dwState = 0;
            Int32 dwProtocol = 0;
            byte[] atr = new byte[64];
            Int32 dwAtrLen = atr.Length;

            var ret = DllApi.SCardStatus(hCard, null, ref cch, ref dwState, ref dwProtocol, atr, ref dwAtrLen);

            if (ret != DllConst.SCARD_S_SUCCESS)
            {
                sb.AddLog($"failure(err code:{GetErrString(ret)})");
                sb.Indent -= 2;
                return (false, sb);
            }
            var state = GetStatus(dwState);
            var protocol = GetProtocol(dwProtocol);
            sb.AddLog($"state:{state}");
            sb.AddLog($"protocol:{protocol}");
            sb.AddLog($"ATR:" + BitConverter.ToString(atr,0, dwAtrLen));
            sb.Indent -= 2;
            return (true, sb);
        }

        static public IEnumerable<string> GetCurrentStatus(UInt32 dwCurrentState)
        {
            var results = new List<string>();
            if ((dwCurrentState & DllConst.SCARD_STATE_UNAWARE) > 0) results.Add("SCARD_STATE_UNAWARE");
            if ((dwCurrentState & DllConst.SCARD_STATE_IGNORE) > 0) results.Add("SCARD_STATE_IGNORE");
            if ((dwCurrentState & DllConst.SCARD_STATE_UNAVAILABLE) > 0) results.Add("SCARD_STATE_UNAVAILABLE");
            if ((dwCurrentState & DllConst.SCARD_STATE_EMPTY) > 0) results.Add("SCARD_STATE_EMPTY");
            if ((dwCurrentState & DllConst.SCARD_STATE_PRESENT) > 0) results.Add("SCARD_STATE_PRESENT");
            if ((dwCurrentState & DllConst.SCARD_STATE_ATRMATCH) > 0) results.Add("SCARD_STATE_ATRMATCH");
            if ((dwCurrentState & DllConst.SCARD_STATE_EXCLUSIVE) > 0) results.Add("SCARD_STATE_EXCLUSIVE");
            if ((dwCurrentState & DllConst.SCARD_STATE_INUSE) > 0) results.Add("SCARD_STATE_INUSE");
            if ((dwCurrentState & DllConst.SCARD_STATE_MUTE) > 0) results.Add("SCARD_STATE_MUTE");
            if ((dwCurrentState & DllConst.SCARD_STATE_UNPOWERED) > 0) results.Add("SCARD_STATE_UNPOWERED");

            return results;
        }

        static public IEnumerable<string> GetEventStatus(UInt32 dwEventStatus)
        {
            var results = new List<string>();
            if ((dwEventStatus & DllConst.SCARD_STATE_IGNORE) > 0) results.Add("SCARD_STATE_IGNORE");
            if ((dwEventStatus & DllConst.SCARD_STATE_CHANGED) > 0) results.Add("SCARD_STATE_CHANGED");
            if ((dwEventStatus & DllConst.SCARD_STATE_UNKNOWN) > 0) results.Add("SCARD_STATE_UNKNOWN");
            if ((dwEventStatus & DllConst.SCARD_STATE_UNAVAILABLE) > 0) results.Add("SCARD_STATE_UNAVAILABLE");
            if ((dwEventStatus & DllConst.SCARD_STATE_EMPTY) > 0) results.Add("SCARD_STATE_EMPTY");
            if ((dwEventStatus & DllConst.SCARD_STATE_PRESENT) > 0) results.Add("SCARD_STATE_PRESENT");
            if ((dwEventStatus & DllConst.SCARD_STATE_ATRMATCH) > 0) results.Add("SCARD_STATE_ATRMATCH");
            if ((dwEventStatus & DllConst.SCARD_STATE_EXCLUSIVE) > 0) results.Add("SCARD_STATE_EXCLUSIVE");
            if ((dwEventStatus & DllConst.SCARD_STATE_INUSE) > 0) results.Add("SCARD_STATE_INUSE");
            if ((dwEventStatus & DllConst.SCARD_STATE_MUTE) > 0) results.Add("SCARD_STATE_MUTE");
            if ((dwEventStatus & DllConst.SCARD_STATE_UNPOWERED) > 0) results.Add("SCARD_STATE_UNPOWERED");

            return results;

        }
        static public string GetStatus(Int32 dwState) => dwState switch
        {
            DllConst.SCARD_ABSENT => nameof(DllConst.SCARD_ABSENT),
            DllConst.SCARD_PRESENT => nameof(DllConst.SCARD_PRESENT),
            DllConst.SCARD_SWALLOWED => nameof(DllConst.SCARD_SWALLOWED),
            DllConst.SCARD_POWERED => nameof(DllConst.SCARD_POWERED),
            DllConst.SCARD_NEGOTIABLE => nameof(DllConst.SCARD_NEGOTIABLE),
            DllConst.SCARD_SPECIFICMODE => nameof(DllConst.SCARD_SPECIFICMODE),
            _ => $"Unknown(0x{dwState:X8})",
        };

        static public string GetProtocol(Int32 dwProtocol) => dwProtocol switch
        {
            DllConst.SCARD_PROTOCOL_RAW => nameof(DllConst.SCARD_PROTOCOL_RAW),
            DllConst.SCARD_PROTOCOL_T0 => nameof(DllConst.SCARD_PROTOCOL_T0),
            DllConst.SCARD_PROTOCOL_T1 => nameof(DllConst.SCARD_PROTOCOL_T1),
            _ => $"Unknown(0x{dwProtocol:X8})",
        };

        static public string GetErrString(UInt32 errCode)
        {
            if (Enum.IsDefined(typeof(ScardErr), errCode))
            {
                return $"{errCode:X} = " + Enum.GetName(typeof(ScardErr), errCode) ?? string.Empty;
            }
            return $"Unknown({errCode:X})";
        }
    }
}
