using System;
using System.Windows.Media;
namespace NoteDesk {
public static class NoteMaterial {
 public static double ClampOpacity(double value) {return double.IsNaN(value)||double.IsInfinity(value)?.88:Math.Max(.25,Math.Min(1,value));}
 public static Brush Background(int color,double opacity,bool highContrast) {
  var start=((SolidColorBrush)UI.Hex(UI.Colors[Math.Abs(color%UI.Colors.Length)])).Color;
  var end=((SolidColorBrush)UI.Hex("#FAF9F6")).Color;
  if(highContrast)start=end;
  start.A=end.A=highContrast?(byte)255:(byte)(ClampOpacity(opacity)*255);
  var gradient=new LinearGradientBrush(start,end,90);gradient.Freeze();return gradient;
 }
}
}
