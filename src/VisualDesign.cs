using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;

namespace NoteDesk {
public static class VisualDesign {
    public static UIElement Navigation(string name,string label) {
        string geometry=name=="active"?"M3,2 L15,2 15,16 3,16 Z M6,6 L12,6 M6,9 L12,9 M6,12 L10,12":
            name=="archived"?"M2,3 L16,3 16,7 2,7 Z M4,7 L4,16 14,16 14,7 M7,10 L11,10":
            name=="deleted"?"M3,5 L15,5 M6,5 L6,2 12,2 12,5 M4,5 L5,16 13,16 14,5 M7,8 L7,13 M11,8 L11,13":
            "M9,1 L11,4 14,3 15,6 17,8 15,11 15,14 12,15 9,17 6,15 3,15 2,12 1,9 3,6 3,3 6,3 Z M9,6 A3,3 0 1 1 8.99,6";
        var icon=new Path {Data=Geometry.Parse(geometry),Width=17,Height=17,Stretch=Stretch.Uniform,StrokeThickness=1.35,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,Margin=new Thickness(0,0,12,0),VerticalAlignment=VerticalAlignment.Center};
        BindingOperations.SetBinding(icon,Path.StrokeProperty,new Binding("Foreground") {RelativeSource=new RelativeSource(RelativeSourceMode.FindAncestor,typeof(Button),1)});
        var row=new StackPanel {Orientation=Orientation.Horizontal};row.Children.Add(icon);row.Children.Add(new TextBlock {Text=label,VerticalAlignment=VerticalAlignment.Center});return row;
    }
    public const string Styles=@"<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
    <Style TargetType='CheckBox'>
      <Setter Property='Foreground' Value='{DynamicResource NoteDeskColor302F2C}'/><Setter Property='Cursor' Value='Hand'/><Setter Property='Padding' Value='9,3'/>
      <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='CheckBox'>
        <Grid Background='Transparent'><Grid.ColumnDefinitions><ColumnDefinition Width='18'/><ColumnDefinition Width='*'/></Grid.ColumnDefinitions>
          <Border x:Name='box' Width='17' Height='17' CornerRadius='5' BorderThickness='1' BorderBrush='{DynamicResource NoteDeskColorDCD8CE}' Background='{DynamicResource NoteDeskColor90FFFFFF}' VerticalAlignment='Center'>
            <Path x:Name='tick' Data='M3,8 L6,11 12,4' Stroke='{DynamicResource NoteDeskColorprimary-ink}' StrokeThickness='1.8' StrokeStartLineCap='Round' StrokeEndLineCap='Round' Visibility='Collapsed'/>
          </Border>
          <ContentPresenter Grid.Column='1' Margin='{TemplateBinding Padding}' VerticalAlignment='Center' RecognizesAccessKey='True'/>
        </Grid>
        <ControlTemplate.Triggers>
          <Trigger Property='IsChecked' Value='True'><Setter TargetName='tick' Property='Visibility' Value='Visible'/><Setter TargetName='box' Property='Background' Value='{DynamicResource NoteDeskColorprimary-fill}'/><Setter TargetName='box' Property='BorderBrush' Value='{DynamicResource NoteDeskColorprimary-fill}'/></Trigger>
          <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='box' Property='BorderBrush' Value='{DynamicResource NoteDeskColor596F60}'/></Trigger>
          <Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='.45'/></Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate></Setter.Value></Setter>
    </Style>
    <Style TargetType='ComboBox'>
      <Setter Property='Foreground' Value='{DynamicResource NoteDeskColor302F2C}'/><Setter Property='Background' Value='{DynamicResource NoteDeskColor90FFFFFF}'/><Setter Property='BorderBrush' Value='{DynamicResource NoteDeskColorDCD8CE}'/><Setter Property='BorderThickness' Value='1'/><Setter Property='MinHeight' Value='36'/><Setter Property='Padding' Value='10,7'/><Setter Property='ScrollViewer.CanContentScroll' Value='True'/>
      <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ComboBox'>
        <Grid>
          <ToggleButton Focusable='False' IsChecked='{Binding IsDropDownOpen,Mode=TwoWay,RelativeSource={RelativeSource TemplatedParent}}' ClickMode='Press' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}'>
            <ToggleButton.Template><ControlTemplate TargetType='ToggleButton'><Border CornerRadius='8' BorderThickness='1' BorderBrush='{TemplateBinding BorderBrush}' Background='{TemplateBinding Background}'><Path Data='M0,0 L4,4 8,0' Width='8' Height='4' Stroke='{DynamicResource NoteDeskColor706D65}' StrokeThickness='1.4' HorizontalAlignment='Right' Margin='0,0,12,0'/></Border></ControlTemplate></ToggleButton.Template>
          </ToggleButton>
          <ContentPresenter x:Name='selection' Content='{TemplateBinding SelectionBoxItem}' ContentTemplate='{TemplateBinding SelectionBoxItemTemplate}' IsHitTestVisible='False' Margin='11,0,32,0' VerticalAlignment='Center'/>
          <TextBox x:Name='PART_EditableTextBox' Visibility='Hidden' Margin='10,0,32,0' Padding='0' Background='Transparent' BorderThickness='0' IsReadOnly='{TemplateBinding IsReadOnly}' VerticalContentAlignment='Center' Focusable='True'/>
          <Popup x:Name='PART_Popup' Placement='Bottom' IsOpen='{TemplateBinding IsDropDownOpen}' AllowsTransparency='True' Focusable='False' PopupAnimation='None'>
            <Border MinWidth='{Binding ActualWidth,RelativeSource={RelativeSource TemplatedParent}}' MaxHeight='280' CornerRadius='9' Background='{DynamicResource NoteDeskColorFFFAF9F6}' BorderBrush='{DynamicResource NoteDeskColorE5E1D8}' BorderThickness='1' Padding='5' Margin='0,4,0,0'><ScrollViewer><ItemsPresenter KeyboardNavigation.DirectionalNavigation='Contained'/></ScrollViewer></Border>
          </Popup>
        </Grid>
        <ControlTemplate.Triggers>
          <Trigger Property='IsEditable' Value='True'><Setter TargetName='PART_EditableTextBox' Property='Visibility' Value='Visible'/><Setter TargetName='selection' Property='Visibility' Value='Hidden'/></Trigger>
          <Trigger Property='IsKeyboardFocusWithin' Value='True'><Setter Property='BorderBrush' Value='{DynamicResource NoteDeskColor596F60}'/></Trigger>
          <Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='.45'/></Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate></Setter.Value></Setter>
    </Style>
    <Style TargetType='ComboBoxItem'>
      <Setter Property='Foreground' Value='{DynamicResource NoteDeskColor302F2C}'/><Setter Property='Padding' Value='10,8'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ComboBoxItem'><Border x:Name='item' CornerRadius='6' Padding='{TemplateBinding Padding}'><ContentPresenter/></Border><ControlTemplate.Triggers><Trigger Property='IsHighlighted' Value='True'><Setter TargetName='item' Property='Background' Value='{DynamicResource NoteDeskColorDFE7DD}'/><Setter Property='Foreground' Value='{DynamicResource NoteDeskColor344F3D}'/></Trigger><Trigger Property='IsSelected' Value='True'><Setter TargetName='item' Property='Background' Value='{DynamicResource NoteDeskColorDFE7DD}'/><Setter Property='Foreground' Value='{DynamicResource NoteDeskColor344F3D}'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
    </Style>
    <Style TargetType='Slider'>
      <Setter Property='MinHeight' Value='22'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Slider'>
        <Grid Background='Transparent' Margin='2,0'><Border Height='4' CornerRadius='2' Background='{DynamicResource NoteDeskColor22596F60}' VerticalAlignment='Center'/>
          <Track x:Name='PART_Track' Orientation='Horizontal' Minimum='{TemplateBinding Minimum}' Maximum='{TemplateBinding Maximum}' Value='{TemplateBinding Value}'>
            <Track.DecreaseRepeatButton><RepeatButton Command='Slider.DecreaseLarge' Focusable='False'><RepeatButton.Template><ControlTemplate><Border Height='4' CornerRadius='2' Background='{DynamicResource NoteDeskColor596F60}' VerticalAlignment='Center'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton>
            <Track.Thumb><Thumb Width='16' Height='18'><Thumb.Template><ControlTemplate><Grid Background='Transparent'><Border Width='14' Height='14' CornerRadius='7' Background='{DynamicResource NoteDeskColor596F60}' BorderBrush='{DynamicResource NoteDeskColorprimary-ink}' BorderThickness='2'/></Grid></ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
            <Track.IncreaseRepeatButton><RepeatButton Command='Slider.IncreaseLarge' Focusable='False' Opacity='0'/></Track.IncreaseRepeatButton>
          </Track>
        </Grid><ControlTemplate.Triggers><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='.4'/></Trigger></ControlTemplate.Triggers>
      </ControlTemplate></Setter.Value></Setter>
    </Style>
    <Style TargetType='DatePickerTextBox' BasedOn='{StaticResource {x:Type TextBox}}'/>
    <Style TargetType='DatePicker'>
      <Setter Property='Foreground' Value='{DynamicResource NoteDeskColor302F2C}'/><Setter Property='Background' Value='{DynamicResource NoteDeskColor90FFFFFF}'/><Setter Property='BorderBrush' Value='{DynamicResource NoteDeskColorDCD8CE}'/><Setter Property='MinHeight' Value='36'/>
      <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='DatePicker'><Border Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='1' CornerRadius='8'><Grid><Grid.ColumnDefinitions><ColumnDefinition/><ColumnDefinition Width='36'/></Grid.ColumnDefinitions><DatePickerTextBox x:Name='PART_TextBox' BorderThickness='0' Background='Transparent' Padding='10,7'/><Button x:Name='PART_Button' Grid.Column='1' Background='Transparent' Margin='0' Padding='8' Focusable='False'><Path Data='M2,4 L16,4 16,17 2,17 Z M5,1 L5,6 M13,1 L13,6 M2,8 L16,8' Width='15' Height='16' Stretch='Uniform' Stroke='{DynamicResource NoteDeskColor706D65}' StrokeThickness='1.4'/></Button><Popup x:Name='PART_Popup' Placement='Bottom' AllowsTransparency='True' StaysOpen='False'/></Grid></Border></ControlTemplate></Setter.Value></Setter>
    </Style>
    </ResourceDictionary>";
}
}
