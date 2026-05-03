using System.Windows;
using System.Windows.Media.Animation;

namespace SteelProgress.App.Animations;

public class GridLengthAnimation : AnimationTimeline
{
    public override Type TargetPropertyType => typeof(GridLength);

    public GridLength From { get; set; }
    public GridLength To { get; set; }

    public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue, AnimationClock animationClock)
    {
        double fromVal = From.Value;
        double toVal = To.Value;

        if (animationClock.CurrentProgress is null)
            return new GridLength(fromVal);

        double progress = animationClock.CurrentProgress.Value;
        double value = fromVal + ((toVal - fromVal) * progress);

        return new GridLength(value);
    }

    protected override Freezable CreateInstanceCore()
    {
        return new GridLengthAnimation();
    }
}