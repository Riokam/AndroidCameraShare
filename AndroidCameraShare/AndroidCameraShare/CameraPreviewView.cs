namespace AndroidCameraShare;

/// <summary>
/// Превью камеры. На Android — TextureView + Camera2, handler создаёт нативную вью сам.
/// </summary>
public sealed class CameraPreviewView : View
{
    public static readonly BindableProperty IsActiveProperty = BindableProperty.Create(
        nameof(IsActive),
        typeof(bool),
        typeof(CameraPreviewView),
        false);

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public event EventHandler<string>? Failed;

    internal void NotifyFailed(string message)
    {
        Failed?.Invoke(this, message);
    }
}
