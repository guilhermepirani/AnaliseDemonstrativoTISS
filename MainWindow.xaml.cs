using System.Windows;

namespace AnaliseDemonstrativoTISS;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private static readonly GridLength LarguraPainelFiltroExpandido = new(1, GridUnitType.Star);
    private static readonly GridLength LarguraPainelResultadoExpandido = new(4, GridUnitType.Star);
    private static readonly Thickness MargemPainelFiltroExpandido = new(0, 0, 14, 0);
    private static readonly Thickness MargemPainelFiltroRecolhido = new(0, 0, 8, 0);
    private static readonly Thickness PaddingPainelFiltroExpandido = new(16);
    private static readonly Thickness PaddingPainelFiltroRecolhido = new(6);

    private const double LarguraPainelFiltroRecolhido = 34;

    public MainWindow()
    {
        InitializeComponent();
        AtualizarEstadoPainelFiltro(estaRecolhido: false);
        AtualizarCamposFiltroPorOperadora();
    }

    private void FilterPanelToggleButton_Checked(object sender, RoutedEventArgs e)
    {
        AtualizarEstadoPainelFiltro(estaRecolhido: true);
    }

    private void FilterPanelToggleButton_Unchecked(object sender, RoutedEventArgs e)
    {
        AtualizarEstadoPainelFiltro(estaRecolhido: false);
    }

    private void AtualizarEstadoPainelFiltro(bool estaRecolhido)
    {
        FilterPanelContent.Visibility = estaRecolhido ? Visibility.Collapsed : Visibility.Visible;
        FilterPanelSubtitleText.Visibility = estaRecolhido ? Visibility.Collapsed : Visibility.Visible;
        FilterPanelBorder.Width = estaRecolhido ? LarguraPainelFiltroRecolhido : double.NaN;
        FilterPanelBorder.Margin = estaRecolhido ? MargemPainelFiltroRecolhido : MargemPainelFiltroExpandido;
        FilterPanelBorder.Padding = estaRecolhido ? PaddingPainelFiltroRecolhido : PaddingPainelFiltroExpandido;
        FilterPanelColumn.Width = estaRecolhido ? GridLength.Auto : LarguraPainelFiltroExpandido;
        ResultPanelColumn.Width = estaRecolhido ? new GridLength(1, GridUnitType.Star) : LarguraPainelResultadoExpandido;
    }
}