using Microsoft.Win32;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using AnaliseDemonstrativoTISS.Operadoras;
using AnaliseDemonstrativoTISS.Operadoras.Amil;
using AnaliseDemonstrativoTISS.Operadoras.Sulamerica;

namespace AnaliseDemonstrativoTISS
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string XmlDialogFilter = "Arquivos XML (*.xml)|*.xml|Arquivos TISS (*.tiss)|*.tiss|Todos os arquivos (*.*)|*.*";
        private const string CsvDialogFilter = "Arquivos CSV (*.csv)|*.csv|Todos os arquivos (*.*)|*.*";
        private const string OperadoraSulamerica = "Sulamerica";
        private const string OperadoraAmil = "Amil";

        private static readonly Dictionary<string, IReadOnlyList<string>> CamposFiltroPorOperadora =
            new(StringComparer.OrdinalIgnoreCase)
            {
                [OperadoraSulamerica] = Enum.GetNames<CampoFiltroSulamerica>(),
                [OperadoraAmil] = Enum.GetNames<CampoFiltroAmil>()
            };

        private static readonly Dictionary<string, Func<RegistroAnalise, string>> MapeamentoColunas =
            new(StringComparer.Ordinal)
            {
                [nameof(RegistroAnalise.Credencial)] = static r => r.Credencial,
                [nameof(RegistroAnalise.Nome)] = static r => r.Nome,
                [nameof(RegistroAnalise.Senha)] = static r => r.Senha,
                [nameof(RegistroAnalise.NumeroGuiaPrestador)] = static r => r.NumeroGuiaPrestador,
                [nameof(RegistroAnalise.NumeroGuiaOperadora)] = static r => r.NumeroGuiaOperadora,
                [nameof(RegistroAnalise.DataAtendimento)] = static r => r.DataAtendimento,
                [nameof(RegistroAnalise.CodigoProcedimento)] = static r => r.CodigoProcedimento,
                [nameof(RegistroAnalise.DescricaoProcedimento)] = static r => r.DescricaoProcedimento,
                [nameof(RegistroAnalise.Quantidade)] = static r => r.Quantidade,
                [nameof(RegistroAnalise.ValorInformado)] = static r => r.ValorInformado,
                [nameof(RegistroAnalise.ValorProcessado)] = static r => r.ValorProcessado,
                [nameof(RegistroAnalise.ValorLiberado)] = static r => r.ValorLiberado,
                [nameof(RegistroAnalise.ValorGlosa)] = static r => r.ValorGlosa,
                [nameof(RegistroAnalise.SituacaoGuia)] = static r => r.SituacaoGuia,
                [nameof(RegistroAnalise.CodigoGlosa)] = static r => r.CodigoGlosa,
                [nameof(RegistroAnalise.DescricaoGlosa)] = static r => r.DescricaoGlosa,
                [nameof(RegistroAnaliseAmil.CodigoGlosaAmil)] = static r => r is RegistroAnaliseAmil amil ? amil.CodigoGlosaAmil : string.Empty,
                [nameof(RegistroAnaliseAmil.DescricaoGlosaAmil)] = static r => r is RegistroAnaliseAmil amil ? amil.DescricaoGlosaAmil : string.Empty
            };

        private static readonly Dictionary<string, int> OrdemColunas = new(StringComparer.Ordinal)
        {
            [nameof(RegistroAnalise.Credencial)] = 0,
            [nameof(RegistroAnalise.Nome)] = 1,
            [nameof(RegistroAnalise.Senha)] = 2,
            [nameof(RegistroAnalise.NumeroGuiaPrestador)] = 3,
            [nameof(RegistroAnalise.NumeroGuiaOperadora)] = 4,
            [nameof(RegistroAnalise.DataAtendimento)] = 5,
            [nameof(RegistroAnalise.CodigoProcedimento)] = 6,
            [nameof(RegistroAnalise.DescricaoProcedimento)] = 7,
            [nameof(RegistroAnalise.Quantidade)] = 8,
            [nameof(RegistroAnalise.ValorInformado)] = 9,
            [nameof(RegistroAnalise.ValorProcessado)] = 10,
            [nameof(RegistroAnalise.ValorLiberado)] = 11,
            [nameof(RegistroAnalise.ValorGlosa)] = 12,
            [nameof(RegistroAnalise.SituacaoGuia)] = 13,
            [nameof(RegistroAnalise.CodigoGlosa)] = 14,
            [nameof(RegistroAnalise.DescricaoGlosa)] = 15,
            [nameof(RegistroAnaliseAmil.CodigoGlosaAmil)] = 16,
            [nameof(RegistroAnaliseAmil.DescricaoGlosaAmil)] = 17
        };

        private readonly AnaliseXml _analisadorSulamerica = new();
        private readonly AnaliseCsv _analisadorAmil = new();
        private IReadOnlyList<RegistroAnalise> _ultimoResultado = [];
        private string _arquivoSelecionado = string.Empty;

        public MainWindow()
        {
            InitializeComponent();
            AtualizarCamposFiltroPorOperadora();
        }

        private void OperadoraComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            AtualizarCamposFiltroPorOperadora();
        }

        private void SelectXmlFileButton_Click(object sender, RoutedEventArgs e)
        {
            var operadora = ObterConteudoSelecionado(OperadoraComboBox);
            var usaCsv = string.Equals(operadora, OperadoraAmil, StringComparison.OrdinalIgnoreCase);

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
                IReadOnlyList<RegistroAnalise> resultado;

                if (string.Equals(operadora, OperadoraSulamerica, StringComparison.OrdinalIgnoreCase))
                {
                    resultado = _analisadorSulamerica.Analisar(_arquivoSelecionado, ObterCampoFiltroSulamerica(), ObterValoresFiltro());
                }
                else if (string.Equals(operadora, OperadoraAmil, StringComparison.OrdinalIgnoreCase))
                {
                    resultado = _analisadorAmil.Analisar(_arquivoSelecionado, ObterCampoFiltroAmil(), ObterValoresFiltro());
                }
                else
                {
                    throw new NotSupportedException($"A análise está disponível no momento apenas para as operadoras {OperadoraSulamerica} e {OperadoraAmil}.");
                }

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

        private CampoFiltroSulamerica ObterCampoFiltroSulamerica()
        {
            var campoSelecionado = ObterConteudoSelecionado(SearchFieldComboBox);
            return Enum.TryParse<CampoFiltroSulamerica>(campoSelecionado, ignoreCase: true, out var campo)
                ? campo
                : CampoFiltroSulamerica.Credencial;
        }

        private CampoFiltroAmil ObterCampoFiltroAmil()
        {
            var campoSelecionado = ObterConteudoSelecionado(SearchFieldComboBox);
            return Enum.TryParse<CampoFiltroAmil>(campoSelecionado, ignoreCase: true, out var campo)
                ? campo
                : CampoFiltroAmil.Credencial;
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

        private void ResultDataGrid_AutoGeneratedColumns(object sender, EventArgs e)
        {
            if (_ultimoResultado.Count == 0)
            {
                return;
            }

            foreach (var coluna in ResultDataGrid.Columns.OfType<DataGridBoundColumn>())
            {
                if (coluna.Binding is not Binding { Path.Path: { } nomePropriedade })
                {
                    continue;
                }

                if (!MapeamentoColunas.TryGetValue(nomePropriedade, out var seletorValor))
                {
                    continue;
                }

                var possuiValor = _ultimoResultado.Any(registro => !string.IsNullOrWhiteSpace(seletorValor(registro)));
                coluna.Visibility = possuiValor ? Visibility.Visible : Visibility.Collapsed;

                if (OrdemColunas.TryGetValue(nomePropriedade, out var ordem))
                {
                    coluna.DisplayIndex = ordem;
                }
            }
        }
    }
}