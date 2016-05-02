using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace BatteryUtils
{
    public partial class FormMain : Form
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern IntPtr CreateFile(
    string filename,
    [MarshalAs(UnmanagedType.U4)] FileAccess desiredAccess,
    [MarshalAs(UnmanagedType.U4)] FileShare shareMode,
    IntPtr securityAttributes,
    [MarshalAs(UnmanagedType.U4)] FileMode creationDisposition,
    [MarshalAs(UnmanagedType.U4)] FILE_ATTRIBUTES flags,
    IntPtr template);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern bool DeviceIoControl(
    IntPtr handle,
    uint controlCode,
    [In] IntPtr inBuffer,
    uint inBufferSize,
    [Out] IntPtr outBuffer,
    uint outBufferSize,
    out uint bytesReturned,
    IntPtr overlapped);

        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, uint Msg, int wParam, int lParam);
        private static readonly IntPtr HWND_BROADCAST = new IntPtr(0xffff);
        private const uint WM_SYSCOMMAND = 0x0112;
        private const int SC_MONITORPOWER = 0xf170;

        [Flags]
        internal enum FILE_ATTRIBUTES : uint
        {
            Readonly = 0x00000001,
            Hidden = 0x00000002,
            System = 0x00000004,
            Directory = 0x00000010,
            Archive = 0x00000020,
            Device = 0x00000040,
            Normal = 0x00000080,
            Temporary = 0x00000100,
            SparseFile = 0x00000200,
            ReparsePoint = 0x00000400,
            Compressed = 0x00000800,
            Offline = 0x00001000,
            NotContentIndexed = 0x00002000,
            Encrypted = 0x00004000,
            Write_Through = 0x80000000,
            Overlapped = 0x40000000,
            NoBuffering = 0x20000000,
            RandomAccess = 0x10000000,
            SequentialScan = 0x08000000,
            DeleteOnClose = 0x04000000,
            BackupSemantics = 0x02000000,
            PosixSemantics = 0x01000000,
            OpenReparsePoint = 0x00200000,
            OpenNoRecall = 0x00100000,
            FirstPipeInstance = 0x00080000
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        internal struct SET_BATTERY_THRESH_IN
        {
            public byte Value;
            public byte BatteryID;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public byte[] Reserved;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        internal struct SET_BATTERY_THRESH_OUT
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public byte[] Reserved;
        }

        internal struct GET_BATTERY_THRESH_IN
        {
            public byte BatteryID;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            public byte[] Reserved;
        }

        internal struct GET_BATTERY_THRESH_OUT
        {
            public byte Value;
            public byte BatteryID;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public byte[] Reserved;
        }

        internal const uint GET_BATTERY_THRESH_START = 0x22262C;
        internal const uint GET_BATTERY_THRESH_STOP = 0x222634;

        internal const uint SET_BATTERY_THRESH_START = 0x222630;
        internal const uint SET_BATTERY_THRESH_STOP = 0x222638;

        internal const byte DEFAULT_BATTERY_ID = 0x01;
        public FormMain()
        {
            InitializeComponent();
        }

        private void monitorOFF_Click(object sender, EventArgs e)
        {
            SendMessage(HWND_BROADCAST, WM_SYSCOMMAND, SC_MONITORPOWER, 2);
        }

        private IntPtr handle = IntPtr.Zero;
        private void applyThreshold_Click(object sender, EventArgs e)
        {
            decimal start = startThreshold.Value;
            decimal stop = stopThreshold.Value;
            //先执行停止指令
            SetBatteryThresh(handle, stop, DEFAULT_BATTERY_ID, SET_BATTERY_THRESH_STOP);
            //再执行开始指令
            SetBatteryThresh(handle, start, DEFAULT_BATTERY_ID, SET_BATTERY_THRESH_START);
        }

        private static bool SetBatteryThresh(IntPtr handle, decimal value, byte id, uint controlCode)
        {
            SET_BATTERY_THRESH_IN batteryThresh = new SET_BATTERY_THRESH_IN();
            batteryThresh.Value = (byte)(value - 1);
            batteryThresh.BatteryID = id;
            int batteryThreshSize = Marshal.SizeOf(batteryThresh);
            IntPtr batteryThreshPointer = Marshal.AllocHGlobal(batteryThreshSize);
            Marshal.StructureToPtr(batteryThresh, batteryThreshPointer, false);

            SET_BATTERY_THRESH_OUT batteryThreshOut = new SET_BATTERY_THRESH_OUT();
            int batteryThreshOutSize = Marshal.SizeOf(batteryThreshOut);
            IntPtr batteryThreshOutPointer = Marshal.AllocHGlobal(batteryThreshOutSize);
            Marshal.StructureToPtr(batteryThreshOut, batteryThreshOutPointer, false);
            try
            {
                return DeviceIoControl(handle, controlCode, batteryThreshSize, batteryThreshPointer, batteryThreshOutSize, batteryThreshOutPointer);
            }
            finally
            {
                Marshal.FreeHGlobal(batteryThreshPointer);
                Marshal.FreeHGlobal(batteryThreshOutPointer);
            }
        }

        private static bool DeviceIoControl(IntPtr handle, uint controlCode, int batteryThreshSize, IntPtr batteryThreshPointer, int batteryThreshOutSize, IntPtr batteryThreshOutPointer)
        {
            uint bytesReturned;
            bool retval = DeviceIoControl(handle, controlCode, batteryThreshPointer, (uint)batteryThreshSize, batteryThreshOutPointer, (uint)batteryThreshOutSize, out bytesReturned, IntPtr.Zero);
            if (!retval)
            {
                int errorCode = Marshal.GetLastWin32Error();
                if (errorCode != 0)
                    throw Marshal.GetExceptionForHR(errorCode);
                else
                    throw new Exception(
                        "DeviceIoControl call failed but Win32 didn't catch an error.");
            }
            return retval;
        }

        private void FormMain_Load(object sender, EventArgs e)
        {
            handle = CreateFile("\\\\.\\IBMPmDrv", FileAccess.ReadWrite, FileShare.ReadWrite, IntPtr.Zero, FileMode.Open, FILE_ATTRIBUTES.Normal, IntPtr.Zero);
            if (handle == IntPtr.Zero || handle.ToInt32() == -1)
            {
                int errorCode = Marshal.GetLastWin32Error();
                if (errorCode != 0)
                    Marshal.ThrowExceptionForHR(errorCode);
                else
                    throw new Exception(
                        "SetupDiGetDeviceInterfaceDetail call failed but Win32 didn't catch an error.");
            }

            GET_BATTERY_THRESH_IN batteryThresh = new GET_BATTERY_THRESH_IN();
            batteryThresh.BatteryID = DEFAULT_BATTERY_ID;
            int batteryThreshSize = Marshal.SizeOf(batteryThresh);
            IntPtr batteryThreshPointer = Marshal.AllocHGlobal(batteryThreshSize);
            Marshal.StructureToPtr(batteryThresh, batteryThreshPointer, false);

            GET_BATTERY_THRESH_OUT batteryThreshOut = new GET_BATTERY_THRESH_OUT();
            int batteryThreshOutSize = Marshal.SizeOf(batteryThreshOut);
            IntPtr batteryThreshOutPointer = Marshal.AllocHGlobal(batteryThreshOutSize);
            Marshal.StructureToPtr(batteryThreshOut, batteryThreshOutPointer, false);
            try
            {

                DeviceIoControl(handle, GET_BATTERY_THRESH_START, batteryThreshSize, batteryThreshPointer, batteryThreshOutSize, batteryThreshOutPointer);
                GET_BATTERY_THRESH_OUT outInfo = (GET_BATTERY_THRESH_OUT)Marshal.PtrToStructure(batteryThreshOutPointer, typeof(GET_BATTERY_THRESH_OUT));
                startThreshold.Value = outInfo.Value + 1;

                DeviceIoControl(handle, GET_BATTERY_THRESH_STOP, batteryThreshSize, batteryThreshPointer, batteryThreshOutSize, batteryThreshOutPointer);
                outInfo = (GET_BATTERY_THRESH_OUT)Marshal.PtrToStructure(batteryThreshOutPointer, typeof(GET_BATTERY_THRESH_OUT));
                stopThreshold.Value = outInfo.Value + 1;
            }
            finally
            {
                Marshal.FreeHGlobal(batteryThreshPointer);
                Marshal.FreeHGlobal(batteryThreshOutPointer);
            }
        }
    }
}
