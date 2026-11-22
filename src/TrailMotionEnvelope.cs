using System.Numerics;

namespace LyricsBridge;

// One direction/length for the whole lyric row, never a radial per-pixel offset.
internal sealed class TrailMotionEnvelope
{
    private Vector2 position, previousDirection = -Vector2.UnitY;
    private double scale, time = double.NaN, visibility;
    private readonly Vector2[] offsets = new Vector2[2];
    private readonly double[] gains = new double[2];
    private int active;
    private double lastLineMotion = double.NegativeInfinity;
    internal double LastMovement { get; private set; } = double.NegativeInfinity;
    internal double Gain => Math.Max(gains[0], gains[1]);
    internal bool LineMoving { get; private set; }

    internal (Vector4 First,Vector4 Second) Update(double now, Vector2 nextPosition, double nextScale,
        Vector2 rhythmTranslation, double font, double nextVisibility, float lengthScale = 1)
    {
        var dt=now-time;
        if(!double.IsFinite(dt)||dt<0||dt>.3)
        {
            position=nextPosition; scale=nextScale; time=now; visibility=nextVisibility;
            gains[0]=gains[1]=0; LastMovement=lastLineMotion=double.NegativeInfinity;
            LineMoving=false; return default;
        }
        if(dt>0)
        {
            var velocity=(nextPosition-position)/(float)dt;
            var speed=velocity.Length(); var scaleSpeed=(nextScale-scale)/dt;
            LineMoving=speed>8||Math.Abs(nextVisibility-visibility)>.001;
            if(LineMoving)lastLineMotion=now;
            var source=rhythmTranslation;
            var translation=source.Length()>1;
            var direction=translation?-Vector2.Normalize(source):new Vector2(0,scaleSpeed>=0?1:-1);
            var drive=Math.Clamp(Math.Max(Math.Abs(scaleSpeed)*9,source.Length()/100),0,1);
            if(drive<.018)drive=0;else drive=.28+.72*drive;
            if(drive>0) LastMovement=now;
            var length=Math.Clamp(font*.08+Math.Max(Math.Abs(scaleSpeed)*font*2.4,source.Length()*.12),font*.08,font*1.45);
            if(drive>0&&Vector2.Dot(previousDirection,direction)<0){active=1-active;previousDirection=direction;}
            else if(drive>0)previousDirection=direction;
            for(var i=0;i<2;i++)
            {
                var target=i==active?drive:0;
                gains[i]=Follow(gains[i],target,dt,target>gains[i]?.045:.18);
                if(gains[i]<.002)gains[i]=0;
            }
            if(drive>0)offsets[active]=Vector2.Lerp(offsets[active],direction*(float)length,(float)(1-Math.Exp(-dt/.07)));
            position=nextPosition;scale=nextScale;time=now;visibility=nextVisibility;
        }
        // Classic motion history already supplies the long cut trail. Bring the
        // short rhythm sweep back gradually, rather than stacking two full tails.
        var gate=Math.Clamp((now-lastLineMotion-.25)/.55,0,1);
        return (new(offsets[0]*lengthScale,(float)(gains[0]*gate),0),new(offsets[1]*lengthScale,(float)(gains[1]*gate),0));
    }

    internal static double FocusGain(double focus) => .7+.3*Math.Clamp(focus,0,1);
    internal static double Follow(double value,double target,double dt,double tau)=>target+(value-target)*Math.Exp(-Math.Max(0,dt)/tau);
}
