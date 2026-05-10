using Microsoft.Win32;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AnaliseDemonstrativoTISS.Operadoras;
using AnaliseDemonstrativoTISS.Operadoras.Amil;
using AnaliseDemonstrativoTISS.Operadoras.Petrobras;
using AnaliseDemonstrativoTISS.Operadoras.Sulamerica;
using AnaliseXmlSulamerica = AnaliseDemonstrativoTISS.Operadoras.Sulamerica.AnaliseXml;

namespace AnaliseDemonstrativoTISS;

public partial class MainWindow
{
    private const string XmlDialogFilter = "Arquivos XML (*.xml)|*.xml|Arquivos TISS (*.tiss)|*.tiss|Todos os arquivos (*.*)|*.*";
    private const string CsvDialogFilter = "Arquivos CSV (*.csv)|*.csv|Todos os arquivos (*.*)|*.*";
    private const string OperadoraSulamerica = "Sulamerica";
    private const string OperadoraPetrobras = "Petrobras";
    private const string OperadoraAmil = "Amil";

    private static readonly Dictionary<string, IReadOnlyList<string>> CamposFiltroPorOperadora =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [OperadoraSulamerica] = Enum.GetNames<CampoFiltroSulamerica>(),
            [OperadoraPetrobras] = Enum.GetNames<CampoFiltroPetrobras>(),
            [OperadoraAmil] = Enum.GetNames<CampoFiltroAmil>()
        };

    private readonly AnaliseXmlSulamerica _analisadorSulamerica = new();
    private readonly Operadoras.Petrobras.AnaliseXml _analisadorPetrobras = new();
    private readonly AnaliseCsv _analisadorAmil = new();
    private string _arquivoSelecionado = string.Empty;

    private void OperadoraComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        AtualizarCamposFiltroPorOperadora();
    }

    private void SelectXmlFileButton_Click(object sender, RoutedEventArgs e)
    {
        var operadora = ObterConteudoSelecionado(OperadoraComboBox);
        var usaCsv = UsaCsv(operadora);

        var dialog = new OpenFileDialog
        {
            Title = usaCsv ? "Selecione um arquivo CSV" : "Selecione um arquivo XML TISS",
            Filter = usaCsv ? CsvDialogFilter : XmlDialogFilter,
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        _arquivoSelecionado = dialog.FileName;
        SelectXmlFileButton.Content = Path.GetFileName(dialog.FileName);
        SelectXmlFileButton.Style = (Style)FindResource("FileSelectedButtonStyle");
    }

    private void ExecuteSearchButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ArquivoSelecionadoValido())
        {
            return;
        }

        try
        {
            var operadora = ObterConteudoSelecionado(OperadoraComboBox);
            var valoresFiltro = ObterValoresFiltro();
            var resultado = ExecutarAnalisePorOperadora(operadora, valoresFiltro);

            _ultimoResultado = resultado;
            ResultDataGrid.ItemsSource = resultado;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Não foi possível executar a busca.\n\n{ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool ArquivoSelecionadoValido()
    {
        if (!string.IsNullOrWhiteSpace(_arquivoSelecionado))
        {
            return true;
        }

        MessageBox.Show(this, "Selecione um arquivo antes de executar a busca.", "Validação", MessageBoxButton.OK, MessageBoxImage.Warning);
        return false;
    }

    private IEnumerable<string> ObterValoresFiltro()
    {
        return FilterValuesTextBox.Text
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(valor => valor.Trim())
            .Where(valor => !string.IsNullOrWhiteSpace(valor));
    }

    private IReadOnlyList<RegistroAnalise> ExecutarAnalisePorOperadora(string operadora, IEnumerable<string> valoresFiltro)
    {
        if (string.Equals(operadora, OperadoraSulamerica, StringComparison.OrdinalIgnoreCase))
        {
            return _analisadorSulamerica.Analisar(_arquivoSelecionado, ObterCampoFiltro(CampoFiltroSulamerica.Credencial), valoresFiltro);
        }

        if (string.Equals(operadora, OperadoraPetrobras, StringComparison.OrdinalIgnoreCase))
        {
            return _analisadorPetrobras.Analisar(_arquivoSelecionado, ObterCampoFiltro(CampoFiltroPetrobras.NumeroGuiaPrestador), valoresFiltro);
        }

        if (string.Equals(operadora, OperadoraAmil, StringComparison.OrdinalIgnoreCase))
        {
            return _analisadorAmil.Analisar(_arquivoSelecionado, ObterCampoFiltro(CampoFiltroAmil.Credencial), valoresFiltro);
        }

        throw new NotSupportedException($"A análise está disponível no momento apenas para as operadoras {OperadoraSulamerica}, {OperadoraPetrobras} e {OperadoraAmil}.");
    }

    private bool UsaCsv(string operadora)
    {
        return string.Equals(operadora, OperadoraAmil, StringComparison.OrdinalIgnoreCase);
    }

    private TCampoFiltro ObterCampoFiltro<TCampoFiltro>(TCampoFiltro campoPadrao)
        where TCampoFiltro : struct, Enum
    {
        var campoSelecionado = ObterConteudoSelecionado(SearchFieldComboBox);
        return Enum.TryParse<TCampoFiltro>(campoSelecionado, ignoreCase: true, out var campo)
            ? campo
            : campoPadrao;
    }

    private void AtualizarCamposFiltroPorOperadora()
    {
        if (OperadoraComboBox is null || SearchFieldComboBox is null)
        {
            return;
        }

        var operadora = ObterConteudoSelecionado(OperadoraComboBox);
        if (!CamposFiltroPorOperadora.TryGetValue(operadora, out var camposFiltro))
        {
            camposFiltro = [];
        }

        SearchFieldComboBox.ItemsSource = camposFiltro;
        SearchFieldComboBox.SelectedIndex = camposFiltro.Count > 0 ? 0 : -1;
    }

    private static string ObterConteudoSelecionado(ComboBox comboBox)
    {
        if (comboBox.SelectedItem is string texto)
        {
            return texto;
        }

        if (comboBox.SelectedItem is ComboBoxItem item)
        {
            return item.Content?.ToString() ?? string.Empty;
        }

        return string.Empty;
    }
}
