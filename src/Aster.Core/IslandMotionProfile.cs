namespace Aster.Core;

/// <summary>Coordinated physical responses; existing spring positions/velocities survive every retarget.</summary>
public sealed record IslandMotionProfile(SpringConfiguration Width,SpringConfiguration Height,SpringConfiguration Shape,SpringConfiguration Content,SpringConfiguration Fade)
{
    public static IslandMotionProfile Create(SpringConfiguration preferences,bool opening)
    {
        var c=preferences.Validated();var ratio=c.Damping/(2*Math.Sqrt(c.Stiffness*c.Mass));
        SpringConfiguration Tune(double speed,double damping)
        {
            var k=c.Stiffness*speed;var z=Math.Clamp(ratio*damping,.72,1.25);
            return new SpringConfiguration(k,2*z*Math.Sqrt(k*c.Mass),c.Mass).Validated();
        }
        return opening
            ?new(Tune(1.16,.84),Tune(1,.90),Tune(1.2,.98),Tune(1.12,1.02),Tune(1.35,1.08))
            :new(Tune(1.28,1.08),Tune(1.38,1.10),Tune(1.4,1.10),Tune(1.15,1.08),Tune(1.4,1.10));
    }
}
