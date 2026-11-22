using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Input;
using System.Windows.Media;
using Aster.Core;

namespace Aster.App;

public sealed class GlassFader:FrameworkElement
{
    public static readonly DependencyProperty ValueProperty=DependencyProperty.Register(nameof(Value),typeof(double),typeof(GlassFader),new FrameworkPropertyMetadata(0d,FrameworkPropertyMetadataOptions.AffectsRender,Changed));
    public static readonly DependencyProperty KindProperty=DependencyProperty.Register(nameof(Kind),typeof(string),typeof(GlassFader),new FrameworkPropertyMetadata("volume",FrameworkPropertyMetadataOptions.AffectsRender));
    public double Value{get=>(double)GetValue(ValueProperty);set=>SetValue(ValueProperty,value);}
    public string Kind{get=>(string)GetValue(KindProperty);set=>SetValue(KindProperty,value);}
    public event EventHandler? ValueCommitted;
    private readonly SpringValue fill=new(0);private bool running,dragging;private long last;
    public GlassFader(){Focusable=true;Cursor=Cursors.Hand;Unloaded+=(_,_)=>{CompositionTarget.Rendering-=Frame;running=false;};}
    private static void Changed(DependencyObject value,DependencyPropertyChangedEventArgs args)
    {
        var self=(GlassFader)value;self.fill.Target=Math.Clamp(double.IsFinite((double)args.NewValue)?(double)args.NewValue:0,0,100);
        if(!self.running){self.running=true;self.last=Stopwatch.GetTimestamp();CompositionTarget.Rendering+=self.Frame;}
    }
    private void Frame(object? sender,EventArgs args)
    {
        var now=Stopwatch.GetTimestamp();var p=(Window.GetWindow(this)?.DataContext as AppModel)?.Preferences;if(p?.ReducedMotion==true)fill.Snap(fill.Target);else fill.Step(Math.Clamp((now-last)/(double)Stopwatch.Frequency,0,.1),NightMotion.Adapt(new(420,38,1),p?.NightMode==true));last=now;InvalidateVisual();
        if(fill.Settled){CompositionTarget.Rendering-=Frame;running=false;}
    }
    protected override void OnRender(DrawingContext dc)
    {
        var r=new Rect(0,0,ActualWidth,ActualHeight);var radius=Math.Min(ActualWidth/2,25);
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(21,255,255,255)),new Pen(new SolidColorBrush(Color.FromArgb(22,255,255,255)),.7),r,radius,radius);
        dc.PushClip(new RectangleGeometry(r,radius,radius));
        var height=r.Height*Math.Clamp(fill.Position/100,0,1);
        dc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(233,233,233),Color.FromRgb(209,209,209),90),null,new Rect(0,r.Height-height,r.Width,height));
        dc.Pop();var brush=new SolidColorBrush(height>32?Color.FromRgb(38,38,38):Color.FromRgb(238,238,238));
        var text=new FormattedText($"{Math.Round(Value):0}%",CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI Semibold"),14,Brushes.White,VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(text,new((r.Width-text.Width)/2,12));
        var icon=new Pen(brush,1.7){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round};var cx=r.Width/2;var cy=r.Height-27;
        if(Kind=="brightness")
        {
            dc.DrawEllipse(null,icon,new(cx,cy),5,5);for(int i=0;i<8;i++){var a=i*Math.PI/4;dc.DrawLine(icon,new(cx+8*Math.Cos(a),cy+8*Math.Sin(a)),new(cx+11*Math.Cos(a),cy+11*Math.Sin(a)));}
        }
        else
        {
            var g=Geometry.Parse("M0,5 L4,5 L9,0 L9,18 L4,13 L0,13 Z M13,4 Q18,9 13,14");dc.PushTransform(new TranslateTransform(cx-8,cy-9));dc.DrawGeometry(null,icon,g);dc.Pop();
        }
        if(IsKeyboardFocused)dc.DrawRoundedRectangle(null,new Pen(new SolidColorBrush(Color.FromRgb(212,212,212)),1.4),new Rect(1,1,r.Width-2,r.Height-2),radius,radius);
    }
    private void Pointer(Point point)=>SetCurrentValue(ValueProperty,Math.Clamp(100*(1-point.Y/Math.Max(1,ActualHeight)),0,100));
    private void Commit()=>ValueCommitted?.Invoke(this,EventArgs.Empty);
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e){base.OnMouseLeftButtonDown(e);if(e.StylusDevice is not null)return;Focus();dragging=true;CaptureMouse();Pointer(e.GetPosition(this));e.Handled=true;}
    protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(dragging&&e.LeftButton==MouseButtonState.Pressed)Pointer(e.GetPosition(this));}
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e){base.OnMouseLeftButtonUp(e);if(!dragging)return;dragging=false;Pointer(e.GetPosition(this));ReleaseMouseCapture();Commit();e.Handled=true;}
    protected override void OnTouchDown(TouchEventArgs e){Focus();dragging=true;CaptureTouch(e.TouchDevice);Pointer(e.GetTouchPoint(this).Position);e.Handled=true;}
    protected override void OnTouchMove(TouchEventArgs e){if(dragging)Pointer(e.GetTouchPoint(this).Position);e.Handled=true;}
    protected override void OnTouchUp(TouchEventArgs e){if(dragging){Pointer(e.GetTouchPoint(this).Position);dragging=false;ReleaseTouchCapture(e.TouchDevice);Commit();}e.Handled=true;}
    protected override void OnKeyDown(KeyEventArgs e)
    {base.OnKeyDown(e);if(e.Key is not(Key.Up or Key.Down or Key.Left or Key.Right or Key.Home or Key.End))return;var value=e.Key==Key.Home?0:e.Key==Key.End?100:Value+(e.Key is Key.Up or Key.Right?2:-2);SetCurrentValue(ValueProperty,Math.Clamp(value,0,100));Commit();e.Handled=true;}
    protected override AutomationPeer OnCreateAutomationPeer()=>new FaderPeer(this);
    private sealed class FaderPeer(GlassFader owner):FrameworkElementAutomationPeer(owner),IRangeValueProvider
    {
        protected override string GetClassNameCore()=>"GlassFader";
        protected override AutomationControlType GetAutomationControlTypeCore()=>AutomationControlType.Slider;
        public override object? GetPattern(PatternInterface pattern)=>pattern==PatternInterface.RangeValue?this:base.GetPattern(pattern);
        public bool IsReadOnly=>!owner.IsEnabled;public double LargeChange=>10;public double SmallChange=>2;public double Minimum=>0;public double Maximum=>100;public double Value=>owner.Value;
        public void SetValue(double value){if(IsReadOnly)throw new ElementNotEnabledException();if(!double.IsFinite(value)||value is <0 or >100)throw new ArgumentOutOfRangeException(nameof(value));owner.SetCurrentValue(ValueProperty,value);owner.Commit();}
    }
}
