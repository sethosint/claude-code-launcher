using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ClaudeLauncher.UI.Models;

public enum LaunchStepStatus
{
    Pending,
    Active,
    Done,
    Skipped,
    Failed
}

/// <summary>
/// A single row in the step-by-step launch checklist shown in the
/// Activity card (e.g. "Load configuration", "Launch Claude").
/// </summary>
public sealed class LaunchStepItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string Label { get; }

    private LaunchStepStatus _status = LaunchStepStatus.Pending;

    public LaunchStepStatus Status
    {
        get => _status;
        set
        {
            if (_status == value)
                return;

            _status = value;

            Raise(nameof(Status));
            Raise(nameof(IsPending));
            Raise(nameof(IsActive));
            Raise(nameof(IsDone));
            Raise(nameof(IsSkipped));
            Raise(nameof(IsFailed));
            Raise(nameof(HasDetail));
        }
    }

    private string _detail = "";

    /// <summary>The live, concrete description of what is happening right now (only shown while Active).</summary>
    public string Detail
    {
        get => _detail;
        set
        {
            if (_detail == value)
                return;

            _detail = value;
            Raise(nameof(Detail));
            Raise(nameof(HasDetail));
        }
    }

    /// <summary>
    /// The detail line is shown while a step is running and kept visible when
    /// it failed (that is when the text matters most — it carries the error).
    /// </summary>
    public bool HasDetail =>
        (IsActive || IsFailed) && !string.IsNullOrWhiteSpace(Detail);

    public bool IsPending => Status == LaunchStepStatus.Pending;
    public bool IsActive => Status == LaunchStepStatus.Active;
    public bool IsDone => Status == LaunchStepStatus.Done;
    public bool IsSkipped => Status == LaunchStepStatus.Skipped;
    public bool IsFailed => Status == LaunchStepStatus.Failed;

    public LaunchStepItem(string label)
    {
        Label = label;
    }

    public void Reset()
    {
        Status = LaunchStepStatus.Pending;
        Detail = "";
    }

    private void Raise([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
