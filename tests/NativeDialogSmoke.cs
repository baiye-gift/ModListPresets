using System;
using ModListPresets;

class NativeDialogSmoke {
    [STAThread]
    static int Main(string[] args) {
        string before = Environment.CurrentDirectory;
        string result = NativeFiles.PickJson(args.Length > 0 ? args[0] : before);
        Console.WriteLine(result == null ? "CANCELLED" : "SELECTED: " + result);
        if (before != Environment.CurrentDirectory) throw new Exception("Dialog changed working directory");
        Console.WriteLine("PASS: working directory preserved");
        return 0;
    }
}
