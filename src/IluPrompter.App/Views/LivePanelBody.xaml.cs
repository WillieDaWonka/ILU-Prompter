using System.Windows;
using System.Windows.Controls;

namespace IluPrompter.App.Views;

public partial class LivePanelBody : UserControl
{
    /// <summary>True in the flyout, whose ground is <c>raised</c>: bound controls then rest on <c>pressed</c>.</summary>
    public static readonly DependencyProperty RaisedGroundProperty = DependencyProperty.Register(
        nameof(RaisedGround), typeof(bool), typeof(LivePanelBody), new PropertyMetadata(false));

    public LivePanelBody() => InitializeComponent();

    public bool RaisedGround
    {
        get => (bool)GetValue(RaisedGroundProperty);
        set => SetValue(RaisedGroundProperty, value);
    }
}
