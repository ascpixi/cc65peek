using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;
using Path = System.IO.Path;

namespace CC65Peek.UI;

public partial class MainWindow : Window
{
    string cc65 = "cc65";
    DateTime srcLastChanged = DateTime.MinValue;
    
    public MainWindow()
    {
        InitializeComponent();
        CompilerParams.Text = "--add-source --cpu 6502";
        SourceInput.Text =
            """
            void main(void) {
                // Write your code here!
                while(1) {}
            }
            """;
        
        var ro = new RegistryOptions(ThemeName.Monokai);
        var sts = SourceInput.InstallTextMate(ro);
        sts.SetGrammar(ro.GetScopeByLanguageId("c"));

        var sto = AsmOutput.InstallTextMate(ro);
        sto.SetGrammar(ro.GetScopeByLanguageId("ini"));
    }

    static bool compiling = false;
    static readonly TimeSpan RateLimit = TimeSpan.FromSeconds(0.25);
    async void OnSourceCodeChanged(object? sender, EventArgs e)
    {
        var p = srcLastChanged;
        srcLastChanged = DateTime.Now;
        
        var timeDelta = DateTime.Now - p;
        if (timeDelta < RateLimit) {
            var prev = srcLastChanged;
            await Task.Delay((RateLimit - timeDelta) * 1.5);

            if (srcLastChanged != prev)
                return; // source code has changed since then
        }

        while (compiling) {
            await Task.Yield();
        }
        
        try {
            compiling = true;

            var tmpSrc = Path.Combine(Path.GetTempPath(), "cc65-src.c");
            var tmpOut = Path.Combine(Path.GetTempPath(), "cc65-out.S");
            await File.WriteAllTextAsync(tmpSrc, SourceInput.Text);

            var proc = Process.Start(new ProcessStartInfo {
                FileName = cc65,
                Arguments = $"{CompilerParams.Text!.Trim()} -o \"{tmpOut}\" \"{tmpSrc}\"",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            });
            
            await proc!.WaitForExitAsync();
            
            if (proc.ExitCode != 0) {
                AsmOutput.Text = (await proc.StandardError.ReadToEndAsync())
                    .Replace(tmpSrc, "<source>");

                AsmOutput.Foreground = Brushes.Crimson;
            }
            else {
                AsmOutput.Text = await File.ReadAllTextAsync(tmpOut);
                File.Delete(tmpOut);
                
                AsmOutput.Foreground = Brushes.White;
            }

            File.Delete(tmpSrc);
        }
        finally {
            compiling = false;
        }
    }

    async void OnLoad(object? sender, RoutedEventArgs e)
    {
        string? cc65Home = Environment.GetEnvironmentVariable("CC65_HOME");
        if (cc65Home != null)
            cc65 = Path.Combine(cc65Home, "bin/cc65");

        if (await OS.IsCallable(cc65, "--version"))
            return;
        
        var result = await StorageProvider.OpenFilePickerAsync(new()
        {
            Title = "Select CC65 path",
            FileTypeFilter = new[] {
                new FilePickerFileType("CC65 compiler") {
                    Patterns = new[] { "cc65.exe" }
                }
            }
        });

        if (result.Count == 0) {
            Close();
            return;
        }

        cc65 = result[0].Path.AbsolutePath;
    }
}