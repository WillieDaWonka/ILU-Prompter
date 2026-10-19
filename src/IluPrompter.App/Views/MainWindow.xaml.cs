using System.Windows;
using IluPrompter.Input;
using IluPrompter.Rendering;

namespace IluPrompter.App.Views;

/// <summary>SCAFFOLD placeholder; stream D owns and replaces.</summary>
public partial class MainWindow : Window
{
    public MainWindow(IAppInputBridge inputBridge, PreviewServices previewServices)
    {
        InitializeComponent();
        inputBridge.AttachWindow(this);
        inputBridge.AttachWheelArea(Preview);
        Preview.Attach(previewServices);
    }
}
