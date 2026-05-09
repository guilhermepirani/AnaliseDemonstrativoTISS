using Microsoft.Win32;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using AnaliseDemonstrativoTISS.Operadoras.Sulamerica;

namespace AnaliseDemonstrativoTISS
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string XmlDialogFilter = "Arquivos XML (*.xml)|*.xml|Arquivos TISS (*.tiss)|*.tiss|Todos os arquivos (*.*)|*.*";
        private const string OperadoraSuportada = "Sulamerica";

        private static readonly Dictionary<string, Func<RegistroAnaliseSulamerica, string>> MapeamentoColunas =
            new(StringComparer.Ordinal)
            {
                [nameof(RegistroAnaliseSulamerica.Credencial)] = static r => r.Credencial,
                [nameof(RegistroAnaliseSulamerica.Nome)] = static r => r.Nome,
                [nameof(RegistroAnaliseSulamerica.Senha)] = static r => r.Senha,
                [nameof(RegistroAnaliseSulamerica.NumeroGuiaPrestador)] = static r => r.NumeroGuiaPrestador,
                [nameof(RegistroAnaliseSulamerica.NumeroGuiaOperadora)] = static r => r.NumeroGuiaOperadora,
                [nameof(RegistroAnaliseSulamerica.DataAtendimento)] = static r => r.DataAtendimento,
                [nameof(RegistroAnaliseSulamerica.CodigoProcedimento)] = static r => r.CodigoProcedimento,
                [nameof(RegistroAnaliseSulamerica.DescricaoProcedimento)] = static r => r.DescricaoProcedimento,
                [nameof(RegistroAnaliseSulamerica.Quantidade)] = static r => r.Quantidade,
                [nameof(RegistroAnaliseSulamerica.ValorInformado)] = static r => r.ValorInformado,
                [nameof(RegistroAnaliseSulamerica.ValorProcessado)] = static r => r.ValorProcessado,
                [nameof(RegistroAnaliseSulamerica.ValorLiberado)] = static r => r.ValorLiberado,
                [nameof(RegistroAnaliseSulamerica.ValorGlosa)] = static r => r.ValorGlosa,
                [nameof(RegistroAnaliseSulamerica.SituacaoGuia)] = static r => r.SituacaoGuia,
                [nameof(RegistroAnaliseSulamerica.CodigoGlosa)] = static r => r.CodigoGlosa,
                [nameof(RegistroAnaliseSulamerica.DescricaoGlosa)] = static r => r.DescricaoGlosa
            };

        private readonly AnaliseXml _analisadorSulamerica = new();
        private IReadOnlyList<RegistroAnaliseSulamerica> _ultimoResultado = [];
        private string _arquivoSelecionado = string.Empty;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void SelectXmlFileButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Selecione um arquivo XML TISS",
                Filter = XmlDialogFilter,
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

            if (!OperadoraSuportadaSelecionada())
            {
                return;
            }

            try
            {
                var resultado = _analisadorSulamerica.Analisar(
                    _arquivoSelecionado,
                    ObterCampoFiltro(),
                    ObterValoresFiltro());

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

            MessageBox.Show(this, "Selecione um arquivo XML antes de executar a busca.", "Validação", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        private bool OperadoraSuportadaSelecionada()
        {
            var operadora = ObterConteudoSelecionado(OperadoraComboBox);
            if (string.Equals(operadora, OperadoraSuportada, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            MessageBox.Show(this, $"A análise está disponível no momento apenas para a operadora {OperadoraSuportada}.", "Operadora não implementada", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        private IEnumerable<string> ObterValoresFiltro()
        {
            return FilterValuesTextBox.Text
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(valor => valor.Trim())
                .Where(valor => !string.IsNullOrWhiteSpace(valor));
        }

        private CampoFiltroSulamerica ObterCampoFiltro()
        {
            var campoSelecionado = ObterConteudoSelecionado(SearchFieldComboBox);
            return campoSelecionado switch
            {
                "Nome" => CampoFiltroSulamerica.Nome,
                "Senha" => CampoFiltroSulamerica.Senha,
                _ => CampoFiltroSulamerica.Credencial
            };
        }

        private static string ObterConteudoSelecionado(ComboBox comboBox)
        {
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
            }
        }
    }
}