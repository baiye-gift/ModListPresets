using System;
using System.IO;
using System.Runtime.InteropServices;

namespace ModListPresets {
    // The dialog owns its native modal message loop on Unity's Windows UI thread.
    // No WinForms, extra managed assemblies, working-directory changes, or shell commands.
    public static class NativeFiles {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct OpenFileName {
            public int size;
            public IntPtr owner, instance;
            public string filter;
            public IntPtr customFilter;
            public int maxCustomFilter, filterIndex;
            public IntPtr file;
            public int maxFile;
            public IntPtr fileTitle;
            public int maxFileTitle;
            public string initialDir, title;
            public int flags;
            public short fileOffset, fileExtension;
            public string defaultExtension;
            public IntPtr customData, hook;
            public string template;
            public IntPtr reserved;
            public int reserved2, flagsEx;
        }
        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetOpenFileNameW(ref OpenFileName data);
        [DllImport("comdlg32.dll")]
        static extern int CommDlgExtendedError();
        [DllImport("user32.dll")]
        static extern IntPtr GetActiveWindow();

        public static string PickJson(string initialDirectory) {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                throw new PlatformNotSupportedException("文件选择器仅支持 Windows，请使用路径输入。");
            var data = new OpenFileName();
            data.size = Marshal.SizeOf(typeof(OpenFileName));
            data.owner = GetActiveWindow();
            data.filter = "模组清单 (*.json)\0*.json\0所有文件 (*.*)\0*.*\0\0";
            data.filterIndex = 1;
            data.maxFile = 32768;
            data.initialDir = Directory.Exists(initialDirectory) ? initialDirectory : null;
            data.title = "导入模组清单";
            data.defaultExtension = "json";
            // Explorer style + must exist + keep CWD + do not add to recent files.
            data.flags = 0x00080000 | 0x00001000 | 0x00000800 | 0x00000008 | 0x02000000;
            data.file = Marshal.AllocHGlobal(data.maxFile * 2);
            try {
                Marshal.WriteInt16(data.file, 0);
                if (GetOpenFileNameW(ref data)) return Marshal.PtrToStringUni(data.file);
                int error = CommDlgExtendedError();
                if (error != 0) throw new IOException("无法打开文件选择器，错误代码：0x" + error.ToString("X"));
                return null;
            } finally { Marshal.FreeHGlobal(data.file); }
        }
    }
}
