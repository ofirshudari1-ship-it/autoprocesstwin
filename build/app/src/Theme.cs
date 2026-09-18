using System;
using System.IO;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;

namespace AutoProcessTwin
{
    // אותה שיטה כמו ב-PureSys: בונים ResourceDictionary ב-runtime מ-XAML string,
    // בלי צורך בפרויקט .csproj/XAML מקומפל - csc.exe גולמי מספיק. פלטת הצבעים
    // (slate כהה + אקסנט אמרלד) זהה בכוונה למוצרים האחרים בתיקייה - עקביות מותג.
    public static class Theme
    {
        public static ResourceDictionary Resources;

        private const string XamlTemplate = @"
<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                     xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>

  <SolidColorBrush x:Key='BgBrush' Color='{BG}'/>
  <SolidColorBrush x:Key='PanelBrush' Color='{PANEL}'/>
  <SolidColorBrush x:Key='Panel2Brush' Color='{PANEL2}'/>
  <SolidColorBrush x:Key='HeaderBgBrush' Color='{HEADERBG}'/>
  <SolidColorBrush x:Key='HeaderTextBrush' Color='{HEADERTEXT}'/>
  <SolidColorBrush x:Key='HeaderSubTextBrush' Color='{HEADERSUB}'/>
  <SolidColorBrush x:Key='AccentBrush' Color='{ACCENT}'/>
  <SolidColorBrush x:Key='AccentHoverBrush' Color='{ACCENTHOVER}'/>
  <SolidColorBrush x:Key='AccentLightBrush' Color='{ACCENTLIGHT}'/>
  <SolidColorBrush x:Key='TextBrush' Color='{TEXT}'/>
  <SolidColorBrush x:Key='TextMutedBrush' Color='{TEXTMUTED}'/>
  <SolidColorBrush x:Key='DangerBrush' Color='{DANGER}'/>
  <SolidColorBrush x:Key='DangerHoverBrush' Color='{DANGERHOVER}'/>
  <SolidColorBrush x:Key='WarningBrush' Color='{WARNING}'/>
  <SolidColorBrush x:Key='BorderColorBrush' Color='{BORDER}'/>
  <SolidColorBrush x:Key='GridAltBrush' Color='{GRIDALT}'/>
  <SolidColorBrush x:Key='WhiteBrush' Color='#FFFFFF'/>

  <!-- STANDARDS §3 requires 4 button states (default/hover/active/disabled) +
       a visible keyboard focus ring - the previous template only had
       hover+disabled. IsPressed now dims to give a distinct 'active' state,
       and a 2px accent-colored focus adorner appears on IsKeyboardFocused
       (not IsFocused, so a mouse click doesn't show a permanent ring - only
       real Tab/keyboard navigation does, matching standard Windows/WCAG
       focus-visible behavior). -->
  <Style x:Key='BaseButtonStyle' TargetType='Button'>
    <Setter Property='Cursor' Value='Hand'/>
    <Setter Property='FontFamily' Value='Segoe UI'/>
    <Setter Property='FontSize' Value='13.5'/>
    <Setter Property='BorderThickness' Value='0'/>
    <Setter Property='Height' Value='38'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='Button'>
          <Grid>
            <Border x:Name='Bd' CornerRadius='9' Background='{TemplateBinding Background}' SnapsToDevicePixels='True'>
              <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center' Margin='14,0,14,0'/>
            </Border>
            <Border x:Name='FocusRing' CornerRadius='11' Margin='-2' BorderThickness='2' BorderBrush='{StaticResource AccentBrush}' Visibility='Collapsed' IsHitTestVisible='False'/>
          </Grid>
          <ControlTemplate.Triggers>
            <Trigger Property='IsEnabled' Value='False'>
              <Setter TargetName='Bd' Property='Opacity' Value='0.5'/>
            </Trigger>
            <Trigger Property='IsPressed' Value='True'>
              <Setter TargetName='Bd' Property='Opacity' Value='0.82'/>
            </Trigger>
            <Trigger Property='IsKeyboardFocused' Value='True'>
              <Setter TargetName='FocusRing' Property='Visibility' Value='Visible'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key='GhostButtonStyle' TargetType='Button' BasedOn='{StaticResource BaseButtonStyle}'>
    <Setter Property='Background' Value='{StaticResource Panel2Brush}'/>
    <Setter Property='Foreground' Value='{StaticResource TextBrush}'/>
    <Style.Triggers>
      <Trigger Property='IsMouseOver' Value='True'>
        <Setter Property='Background' Value='{StaticResource BorderColorBrush}'/>
      </Trigger>
    </Style.Triggers>
  </Style>

  <Style x:Key='AccentButtonStyle' TargetType='Button' BasedOn='{StaticResource BaseButtonStyle}'>
    <Setter Property='Background' Value='{StaticResource AccentBrush}'/>
    <Setter Property='Foreground' Value='{StaticResource WhiteBrush}'/>
    <Setter Property='FontWeight' Value='SemiBold'/>
    <Style.Triggers>
      <Trigger Property='IsMouseOver' Value='True'>
        <Setter Property='Background' Value='{StaticResource AccentHoverBrush}'/>
      </Trigger>
    </Style.Triggers>
  </Style>

  <Style x:Key='DangerButtonStyle' TargetType='Button' BasedOn='{StaticResource BaseButtonStyle}'>
    <Setter Property='Background' Value='{StaticResource DangerBrush}'/>
    <Setter Property='Foreground' Value='{StaticResource WhiteBrush}'/>
    <Setter Property='FontWeight' Value='SemiBold'/>
    <Style.Triggers>
      <Trigger Property='IsMouseOver' Value='True'>
        <Setter Property='Background' Value='{StaticResource DangerHoverBrush}'/>
      </Trigger>
    </Style.Triggers>
  </Style>

  <Style x:Key='IconButtonStyle' TargetType='Button' BasedOn='{StaticResource BaseButtonStyle}'>
    <Setter Property='Background' Value='Transparent'/>
    <Setter Property='Foreground' Value='{StaticResource TextMutedBrush}'/>
    <Setter Property='Height' Value='30'/>
    <Style.Triggers>
      <Trigger Property='IsMouseOver' Value='True'>
        <Setter Property='Background' Value='{StaticResource Panel2Brush}'/>
        <Setter Property='Foreground' Value='{StaticResource TextBrush}'/>
      </Trigger>
    </Style.Triggers>
  </Style>

  <Style x:Key='ModernTabItemStyle' TargetType='TabItem'>
    <Setter Property='FontFamily' Value='Segoe UI'/>
    <Setter Property='FontSize' Value='13.5'/>
    <Setter Property='FontWeight' Value='SemiBold'/>
    <Setter Property='Padding' Value='18,10,18,10'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='TabItem'>
          <Border x:Name='Bd' CornerRadius='10,10,0,0' Margin='2,4,2,0' Background='{StaticResource Panel2Brush}'>
            <ContentPresenter x:Name='Cp' ContentSource='Header' HorizontalAlignment='Center' VerticalAlignment='Center' Margin='{TemplateBinding Padding}'/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsSelected' Value='True'>
              <Setter TargetName='Bd' Property='Background' Value='{StaticResource AccentBrush}'/>
              <Setter Property='Foreground' Value='{StaticResource WhiteBrush}'/>
            </Trigger>
            <Trigger Property='IsSelected' Value='False'>
              <Setter Property='Foreground' Value='{StaticResource TextBrush}'/>
            </Trigger>
            <Trigger Property='IsMouseOver' Value='True'>
              <Setter TargetName='Bd' Property='Background' Value='{StaticResource BorderColorBrush}'/>
            </Trigger>
            <MultiTrigger>
              <MultiTrigger.Conditions>
                <Condition Property='IsMouseOver' Value='True'/>
                <Condition Property='IsSelected' Value='True'/>
              </MultiTrigger.Conditions>
              <Setter TargetName='Bd' Property='Background' Value='{StaticResource AccentHoverBrush}'/>
            </MultiTrigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key='ModernTabControlStyle' TargetType='TabControl'>
    <Setter Property='Background' Value='{StaticResource BgBrush}'/>
    <Setter Property='BorderThickness' Value='0'/>
    <Setter Property='Padding' Value='0'/>
    <Setter Property='ItemContainerStyle' Value='{StaticResource ModernTabItemStyle}'/>
  </Style>

  <Style x:Key='ModernListBoxStyle' TargetType='ListBox'>
    <Setter Property='Background' Value='{StaticResource PanelBrush}'/>
    <Setter Property='Foreground' Value='{StaticResource TextBrush}'/>
    <Setter Property='BorderThickness' Value='1'/>
    <Setter Property='BorderBrush' Value='{StaticResource BorderColorBrush}'/>
    <Setter Property='FontFamily' Value='Segoe UI'/>
    <Setter Property='FontSize' Value='13'/>
    <Setter Property='Padding' Value='4'/>
  </Style>

  <Style x:Key='ModernTextBoxStyle' TargetType='TextBox'>
    <Setter Property='Background' Value='{StaticResource Panel2Brush}'/>
    <Setter Property='Foreground' Value='{StaticResource TextBrush}'/>
    <Setter Property='CaretBrush' Value='{StaticResource TextBrush}'/>
    <Setter Property='BorderBrush' Value='{StaticResource BorderColorBrush}'/>
    <Setter Property='BorderThickness' Value='1'/>
    <Setter Property='Padding' Value='8,6,8,6'/>
    <Setter Property='FontFamily' Value='Segoe UI'/>
    <Setter Property='FontSize' Value='13'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='TextBox'>
          <Border CornerRadius='7' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}'>
            <ScrollViewer x:Name='PART_ContentHost' Margin='{TemplateBinding Padding}' VerticalAlignment='Center'/>
          </Border>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key='ModernPasswordBoxStyle' TargetType='PasswordBox'>
    <Setter Property='Background' Value='{StaticResource Panel2Brush}'/>
    <Setter Property='Foreground' Value='{StaticResource TextBrush}'/>
    <Setter Property='CaretBrush' Value='{StaticResource TextBrush}'/>
    <Setter Property='BorderBrush' Value='{StaticResource BorderColorBrush}'/>
    <Setter Property='BorderThickness' Value='1'/>
    <Setter Property='Padding' Value='8,6,8,6'/>
    <Setter Property='FontFamily' Value='Segoe UI'/>
    <Setter Property='FontSize' Value='13'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='PasswordBox'>
          <Border CornerRadius='7' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}'>
            <ScrollViewer x:Name='PART_ContentHost' Margin='{TemplateBinding Padding}' VerticalAlignment='Center'/>
          </Border>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key='ModernComboBoxItemStyle' TargetType='ComboBoxItem'>
    <Setter Property='Padding' Value='10,7,10,7'/>
    <Setter Property='Foreground' Value='{StaticResource TextBrush}'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='ComboBoxItem'>
          <Border x:Name='Bd' Background='Transparent' Padding='{TemplateBinding Padding}'>
            <ContentPresenter/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsHighlighted' Value='True'>
              <Setter TargetName='Bd' Property='Background' Value='{StaticResource AccentBrush}'/>
              <Setter Property='Foreground' Value='{StaticResource WhiteBrush}'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key='ModernComboBoxStyle' TargetType='ComboBox'>
    <Setter Property='Background' Value='{StaticResource Panel2Brush}'/>
    <Setter Property='Foreground' Value='{StaticResource TextBrush}'/>
    <Setter Property='BorderBrush' Value='{StaticResource BorderColorBrush}'/>
    <Setter Property='BorderThickness' Value='1'/>
    <Setter Property='FontFamily' Value='Segoe UI'/>
    <Setter Property='FontSize' Value='13'/>
    <Setter Property='Height' Value='38'/>
    <Setter Property='Padding' Value='10,0,10,0'/>
    <Setter Property='ItemContainerStyle' Value='{StaticResource ModernComboBoxItemStyle}'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='ComboBox'>
          <Grid>
            <ToggleButton x:Name='Toggle'
                          IsChecked='{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}'
                          ClickMode='Press' Focusable='False' Background='Transparent'>
              <ToggleButton.Template>
                <ControlTemplate TargetType='ToggleButton'>
                  <Border CornerRadius='7' Background='{Binding Background, RelativeSource={RelativeSource AncestorType=ComboBox}}'
                          BorderBrush='{Binding BorderBrush, RelativeSource={RelativeSource AncestorType=ComboBox}}'
                          BorderThickness='{Binding BorderThickness, RelativeSource={RelativeSource AncestorType=ComboBox}}'>
                    <Grid>
                      <Grid.ColumnDefinitions>
                        <ColumnDefinition Width='*'/>
                        <ColumnDefinition Width='28'/>
                      </Grid.ColumnDefinitions>
                      <Path Grid.Column='1' Data='M 0 0 L 4 5 L 8 0 Z' Fill='{StaticResource TextMutedBrush}'
                            HorizontalAlignment='Center' VerticalAlignment='Center'/>
                    </Grid>
                  </Border>
                </ControlTemplate>
              </ToggleButton.Template>
            </ToggleButton>
            <ContentPresenter x:Name='ContentSite' IsHitTestVisible='False'
                              Content='{TemplateBinding SelectionBoxItem}'
                              ContentTemplate='{TemplateBinding SelectionBoxItemTemplate}'
                              Margin='{TemplateBinding Padding}'
                              VerticalAlignment='Center' HorizontalAlignment='Stretch'/>
            <Popup x:Name='Popup' Placement='Bottom' IsOpen='{TemplateBinding IsDropDownOpen}'
                   AllowsTransparency='True' Focusable='False' PopupAnimation='Slide'>
              <Border Background='{StaticResource PanelBrush}' BorderBrush='{StaticResource BorderColorBrush}'
                      BorderThickness='1' CornerRadius='7' MinWidth='{Binding ActualWidth, RelativeSource={RelativeSource AncestorType=ComboBox}}'
                      MaxHeight='220' Margin='0,4,0,0'>
                <ScrollViewer Margin='2'>
                  <ItemsPresenter/>
                </ScrollViewer>
              </Border>
            </Popup>
          </Grid>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key='SectionLabelStyle' TargetType='TextBlock'>
    <Setter Property='FontFamily' Value='Segoe UI'/>
    <Setter Property='FontSize' Value='16'/>
    <Setter Property='FontWeight' Value='Bold'/>
    <Setter Property='Foreground' Value='{StaticResource TextBrush}'/>
    <Setter Property='Margin' Value='0,0,0,12'/>
  </Style>

  <Style x:Key='HintLabelStyle' TargetType='TextBlock'>
    <Setter Property='FontFamily' Value='Segoe UI'/>
    <Setter Property='FontSize' Value='12'/>
    <Setter Property='Foreground' Value='{StaticResource TextMutedBrush}'/>
    <Setter Property='TextWrapping' Value='Wrap'/>
  </Style>

  <Style x:Key='CardBorderStyle' TargetType='Border'>
    <Setter Property='Background' Value='{StaticResource PanelBrush}'/>
    <Setter Property='BorderBrush' Value='{StaticResource BorderColorBrush}'/>
    <Setter Property='BorderThickness' Value='1'/>
    <Setter Property='CornerRadius' Value='12'/>
    <Setter Property='Padding' Value='18'/>
    <!-- Elevation (STANDARDS §15.1 - Fluent 'layering', card surface above
         background) - a soft shadow instead of relying on the border alone
         to separate the card from the page behind it. -->
    <Setter Property='Effect'>
      <Setter.Value>
        <DropShadowEffect Color='#000000' Direction='270' ShadowDepth='2' BlurRadius='10' Opacity='0.18'/>
      </Setter.Value>
    </Setter>
  </Style>

</ResourceDictionary>";

        public static string CurrentThemeName = "Dark";

        public static void Load(string themeName)
        {
            CurrentThemeName = themeName;
            string xaml;
            if (themeName == "Dark")
            {
                xaml = XamlTemplate
                    .Replace("{BG}", "#0F172A").Replace("{PANEL}", "#1E293B").Replace("{PANEL2}", "#273349")
                    .Replace("{HEADERBG}", "#0B1220").Replace("{HEADERTEXT}", "#F1F5F9").Replace("{HEADERSUB}", "#94A3B8")
                    .Replace("{ACCENT}", "#10B981").Replace("{ACCENTHOVER}", "#34D399").Replace("{ACCENTLIGHT}", "#0F3D30")
                    .Replace("{TEXT}", "#E2E8F0").Replace("{TEXTMUTED}", "#94A3B8")
                    .Replace("{DANGER}", "#F87171").Replace("{DANGERHOVER}", "#EF4444").Replace("{WARNING}", "#FBBF24")
                    .Replace("{BORDER}", "#334155").Replace("{GRIDALT}", "#19233A");
            }
            else
            {
                xaml = XamlTemplate
                    .Replace("{BG}", "#F1F5F9").Replace("{PANEL}", "#FFFFFF").Replace("{PANEL2}", "#E9EEF6")
                    .Replace("{HEADERBG}", "#0F2E27").Replace("{HEADERTEXT}", "#FFFFFF").Replace("{HEADERSUB}", "#8FBFB0")
                    .Replace("{ACCENT}", "#10B981").Replace("{ACCENTHOVER}", "#059669").Replace("{ACCENTLIGHT}", "#D1FAE5")
                    .Replace("{TEXT}", "#1E293B").Replace("{TEXTMUTED}", "#64748B")
                    .Replace("{DANGER}", "#EF4444").Replace("{DANGERHOVER}", "#DC2626").Replace("{WARNING}", "#D97706")
                    .Replace("{BORDER}", "#E2E8F0").Replace("{GRIDALT}", "#F8FAFC");
            }
            using (var stringReader = new StringReader(xaml))
            using (var xmlReader = System.Xml.XmlReader.Create(stringReader))
            {
                Resources = (ResourceDictionary)XamlReader.Load(xmlReader);
            }
        }

        public static Brush Get(string key) { return (Brush)Resources[key]; }
        public static object GetStyle(string key) { return Resources[key]; }
    }
}
