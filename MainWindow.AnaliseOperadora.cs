using Microsoft.Win32;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AnaliseDemonstrativoTISS.Operadoras;
using AnaliseDemonstrativoTISS.Operadoras.Amil;
using AnaliseDemonstrativoTISS.Operadoras.Cabesp;
using AnaliseDemonstrativoTISS.Operadoras.CaixaDeCubatao;
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
    private const string OperadoraCabesp = "Cabesp";
    private const string OperadoraCaixaCubatao = "Caixa de Cubatão";

    private static readonly Dictionary<string, IReadOnlyList<string>> CamposFiltroPorOperadora =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [OperadoraSulamerica] = Enum.GetNames<CampoFiltroSulamerica>().OrderBy(campo => campo, StringComparer.CurrentCultureIgnoreCase).ToArray(),
            [OperadoraPetrobras] = Enum.GetNames<CampoFiltroPetrobras>().OrderBy(campo => campo, StringComparer.CurrentCultureIgnoreCase).ToArray(),
            [OperadoraAmil] = Enum.GetNames<CampoFiltroAmil>().OrderBy(campo => campo, StringComparer.CurrentCultureIgnoreCase).ToArray(),
            [OperadoraCabesp] = Enum.GetNames<CampoFiltroCabesp>().OrderBy(campo => campo, StringComparer.CurrentCultureIgnoreCase).ToArray(),
            [OperadoraCaixaCubatao] = Enum.GetNames<CampoFiltroCaixaCubatao>().OrderBy(campo => campo, StringComparer.CurrentCultureIgnoreCase).ToArray()
        };

    private readonly AnaliseXmlSulamerica _analisadorSulamerica = new();
    private readonly Operadoras.Petrobras.AnaliseXml _analisadorPetrobras = new();
    private readonly AnaliseCsv _analisadorAmil = new();
    private readonly Operadoras.Cabesp.AnaliseXml _analisadorCabesp = new();
    private readonly Operadoras.CaixaDeCubatao.AnaliseXml _analisadorCaixaCubatao = new();
    private IReadOnlyList<string> _arquivosSelecionados = [];

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
            Multiselect = true
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        _arquivosSelecionados = dialog.FileNames;
        SelectXmlFileButton.Content = ObterDescricaoArquivosSelecionados(_arquivosSelecionados);
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
            var resultado = ExecutarAnalisePorOperadora(operadora, valoresFiltro, _arquivosSelecionados);

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
        if (_arquivosSelecionados.Count > 0)
        {
            return true;
        }

        MessageBox.Show(this, "Selecione ao menos um arquivo antes de executar a busca.", "Validação", MessageBoxButton.OK, MessageBoxImage.Warning);
        return false;
    }

    private IEnumerable<string> ObterValoresFiltro()
    {
        return FilterValuesTextBox.Text
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(valor => valor.Trim())
            .Where(valor => !string.IsNullOrWhiteSpace(valor));
    }

    private IReadOnlyList<RegistroAnalise> ExecutarAnalisePorOperadora(string operadora, IEnumerable<string> valoresFiltro, IReadOnlyList<string> arquivos)
    {
        var resultadoConsolidado = new List<RegistroAnalise>();
        var exibirNomeArquivo = arquivos.Count > 1;

        foreach (var arquivo in arquivos)
        {
            var resultadoArquivo = ExecutarAnaliseArquivo(operadora, valoresFiltro, arquivo);
            var nomeArquivo = exibirNomeArquivo ? Path.GetFileName(arquivo) : string.Empty;

            foreach (var registro in resultadoArquivo)
            {
                registro.Arquivo = nomeArquivo;
                resultadoConsolidado.Add(registro);
            }
        }

        return resultadoConsolidado;
    }

    private IReadOnlyList<RegistroAnalise> ExecutarAnaliseArquivo(string operadora, IEnumerable<string> valoresFiltro, string arquivo)
    {
        if (string.Equals(operadora, OperadoraSulamerica, StringComparison.OrdinalIgnoreCase))
        {
            return _analisadorSulamerica.Analisar(arquivo, ObterCampoFiltro(CampoFiltroSulamerica.Credencial), valoresFiltro);
        }

        if (string.Equals(operadora, OperadoraPetrobras, StringComparison.OrdinalIgnoreCase))
        {
            return _analisadorPetrobras.Analisar(arquivo, ObterCampoFiltro(CampoFiltroPetrobras.NumeroGuiaPrestador), valoresFiltro);
        }

        if (string.Equals(operadora, OperadoraAmil, StringComparison.OrdinalIgnoreCase))
        {
            return _analisadorAmil.Analisar(arquivo, ObterCampoFiltro(CampoFiltroAmil.Credencial), valoresFiltro);
        }

        if (string.Equals(operadora, OperadoraCabesp, StringComparison.OrdinalIgnoreCase))
        {
            return _analisadorCabesp.Analisar(arquivo, ObterCampoFiltro(CampoFiltroCabesp.Credencial), valoresFiltro);
        }

        if (string.Equals(operadora, OperadoraCaixaCubatao, StringComparison.OrdinalIgnoreCase))
        {
            return _analisadorCaixaCubatao.Analisar(arquivo, ObterCampoFiltro(CampoFiltroCaixaCubatao.Credencial), valoresFiltro);
        }

        throw new NotSupportedException($"A análise está disponível no momento apenas para as operadoras {OperadoraSulamerica}, {OperadoraPetrobras}, {OperadoraAmil}, {OperadoraCabesp} e {OperadoraCaixaCubatao}.");
    }

    private static string ObterDescricaoArquivosSelecionados(IReadOnlyList<string> arquivosSelecionados)
    {
        if (arquivosSelecionados.Count == 0)
        {
            return "Selecionar arquivo";
        }

        var primeiroArquivo = Path.GetFileName(arquivosSelecionados[0]);
        var quantidadeArquivosExtras = arquivosSelecionados.Count - 1;

        return quantidadeArquivosExtras > 0
            ? $"{primeiroArquivo} +{quantidadeArquivosExtras}"
            : primeiroArquivo;
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
