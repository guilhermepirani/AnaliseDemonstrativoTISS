using Microsoft.Win32;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
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

        private static readonly HashSet<string> ColunasAlinhadasEsquerda =
            [nameof(RegistroAnalise.Nome), nameof(RegistroAnalise.DescricaoGlosa), nameof(RegistroAnalise.DescricaoProcedimento)];

        private readonly AnaliseXml _analisadorSulamerica = new();
        private readonly AnaliseCsv _analisadorAmil = new();
        private IReadOnlyList<RegistroAnalise> _ultimoResultado = [];
        private string _arquivoSelecionado = string.Empty;
        private bool _resultadoEhAmil;
        private TipoSelecaoAtual _tipoSelecaoAtual = TipoSelecaoAtual.Nenhuma;
        private int? _ultimoIndiceLinhaSelecionada;
        private int? _ultimoIndiceColunaSelecionada;

        private enum TipoSelecaoAtual
        {
            Nenhuma,
            Linha,
            Coluna
        }

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
                    _resultadoEhAmil = false;
                }
                else if (string.Equals(operadora, OperadoraAmil, StringComparison.OrdinalIgnoreCase))
                {
                    resultado = _analisadorAmil.Analisar(_arquivoSelecionado, ObterCampoFiltroAmil(), ObterValoresFiltro());
                    _resultadoEhAmil = true;
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

        private void ResultDataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            e.Row.Tag = (e.Row.GetIndex() + 1).ToString();
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
            ConfigurarColunaSequencial();

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

                var alinharEsquerda = DeveAlinharEsquerda(nomePropriedade);
                coluna.CellStyle = (Style)FindResource(alinharEsquerda ? "LeftAlignedDataGridCellStyle" : "ResultDataGridCellStyle");
                coluna.HeaderStyle = (Style)FindResource(alinharEsquerda ? "LeftAlignedDataGridHeaderStyle" : "ResultDataGridHeaderStyle");

                if (!MapeamentoColunas.TryGetValue(nomePropriedade, out var seletorValor))
                {
                    continue;
                }

                var possuiValor = _ultimoResultado.Any(registro => !string.IsNullOrWhiteSpace(seletorValor(registro)));
                coluna.Visibility = possuiValor ? Visibility.Visible : Visibility.Collapsed;

                if (OrdemColunas.TryGetValue(nomePropriedade, out var ordem))
                {
                    coluna.DisplayIndex = ordem + 1;
                }

                if (coluna is DataGridTextColumn colunaTexto)
                {
                    colunaTexto.ElementStyle = (Style)FindResource(alinharEsquerda ? "LeftAlignedDataGridTextStyle" : "CenteredDataGridTextStyle");
                }
            }
        }

        private void ConfigurarColunaSequencial()
        {
            var colunaSequencial = ResultDataGrid.Columns.OfType<DataGridTemplateColumn>().FirstOrDefault();
            if (colunaSequencial is null)
            {
                return;
            }

            colunaSequencial.DisplayIndex = 0;
        }

        private bool DeveAlinharEsquerda(string nomePropriedade)
        {
            return ColunasAlinhadasEsquerda.Contains(nomePropriedade)
                || (_resultadoEhAmil && string.Equals(nomePropriedade, nameof(RegistroAnaliseAmil.DescricaoGlosaAmil), StringComparison.Ordinal));
        }

        private void CopyAllDataButton_Click(object sender, RoutedEventArgs e)
        {
            CopiarTodosDados();
        }

        private void ResultDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.C || Keyboard.Modifiers != ModifierKeys.Control)
            {
                return;
            }

            if (!CopiarSelecaoAtual())
            {
                CopiarTodosDados();
            }

            e.Handled = true;
        }

        private bool CopiarSelecaoAtual()
        {
            var celulasSelecionadas = ResultDataGrid.SelectedCells
                .Where(celula => celula.Column is DataGridBoundColumn)
                .Where(celula => celula.Item is RegistroAnalise)
                .Where(celula => celula.Column.Visibility == Visibility.Visible)
                .ToList();

            if (celulasSelecionadas.Count == 0)
            {
                return false;
            }

            var colunasSelecionadas = celulasSelecionadas
                .Select(celula => celula.Column)
                .Distinct()
                .OrderBy(coluna => coluna.DisplayIndex)
                .OfType<DataGridBoundColumn>()
                .ToList();

            if (colunasSelecionadas.Count == 0)
            {
                return false;
            }

            var linhasSelecionadas = celulasSelecionadas
                .Select(celula => ResultDataGrid.Items.IndexOf(celula.Item))
                .Where(indice => indice >= 0)
                .Distinct()
                .OrderBy(indice => indice)
                .ToList();

            if (linhasSelecionadas.Count == 0)
            {
                return false;
            }

            var chavesSelecionadas = celulasSelecionadas
                .Select(celula => (Linha: ResultDataGrid.Items.IndexOf(celula.Item), Coluna: celula.Column.DisplayIndex))
                .Where(chave => chave.Linha >= 0)
                .ToHashSet();

            var caminhosColunas = colunasSelecionadas
                .Select(coluna => new
                {
                    coluna.DisplayIndex,
                    NomePropriedade = (coluna.Binding as Binding)?.Path?.Path ?? string.Empty
                })
                .ToList();

            var conteudo = new StringBuilder();

            foreach (var indiceLinha in linhasSelecionadas)
            {
                if (ObterItemPorIndice(indiceLinha) is not RegistroAnalise registro)
                {
                    continue;
                }

                var valores = caminhosColunas.Select(coluna =>
                {
                    if (!chavesSelecionadas.Contains((indiceLinha, coluna.DisplayIndex)))
                    {
                        return string.Empty;
                    }

                    return SanitizarTextoParaClipboard(ObterValorColuna(registro, coluna.NomePropriedade));
                });

                conteudo.AppendLine(string.Join('\t', valores));
            }

            if (conteudo.Length == 0)
            {
                return false;
            }

            Clipboard.SetText(conteudo.ToString());
            return true;
        }

        private void ResultDataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is not DependencyObject source)
            {
                return;
            }

            var colunaCabecalho = ObterAncestor<DataGridColumnHeader>(source);
            if (colunaCabecalho?.Column is null)
            {
                return;
            }

            if (colunaCabecalho.Column is DataGridTemplateColumn)
            {
                return;
            }

            if (e.ClickCount >= 2)
            {
                OrdenarPorColuna(colunaCabecalho.Column);
            }
            else
            {
                SelecionarColuna(colunaCabecalho.Column, Keyboard.Modifiers);
            }

            e.Handled = true;
        }

        private void RowNumberCell_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not DependencyObject source)
            {
                return;
            }

            var linha = ObterAncestor<DataGridRow>(source);
            if (linha is null)
            {
                return;
            }

            var indiceLinha = ResultDataGrid.Items.IndexOf(linha.Item);
            if (indiceLinha < 0)
            {
                return;
            }

            DefinirCelulaAtualDaLinha(indiceLinha);
            SelecionarLinha(indiceLinha, Keyboard.Modifiers);
            e.Handled = true;
        }

        private void SelecionarLinha(int indiceLinha, ModifierKeys modificadores)
        {
            var item = ObterItemPorIndice(indiceLinha);
            if (item is null)
            {
                return;
            }

            if (_tipoSelecaoAtual == TipoSelecaoAtual.Coluna)
            {
                LimparSelecao();
            }

            _tipoSelecaoAtual = TipoSelecaoAtual.Linha;

            if (!modificadores.HasFlag(ModifierKeys.Control) && !modificadores.HasFlag(ModifierKeys.Shift))
            {
                LimparSelecao();
                SelecionarLinhaCompleta(item);
                _ultimoIndiceLinhaSelecionada = indiceLinha;
                return;
            }

            if (modificadores.HasFlag(ModifierKeys.Shift) && _ultimoIndiceLinhaSelecionada.HasValue)
            {
                var inicio = Math.Min(_ultimoIndiceLinhaSelecionada.Value, indiceLinha);
                var fim = Math.Max(_ultimoIndiceLinhaSelecionada.Value, indiceLinha);

                for (var i = inicio; i <= fim; i++)
                {
                    var itemFaixa = ObterItemPorIndice(i);
                    if (itemFaixa is not null)
                    {
                        SelecionarLinhaCompleta(itemFaixa);
                    }
                }
            }
            else
            {
                SelecionarLinhaCompleta(item);
            }

            _ultimoIndiceLinhaSelecionada = indiceLinha;
        }

        private void DefinirCelulaAtualDaLinha(int indiceLinha)
        {
            var item = ObterItemPorIndice(indiceLinha);
            if (item is null)
            {
                return;
            }

            var primeiraColunaDados = ResultDataGrid.Columns
                .Where(c => c.Visibility == Visibility.Visible && c is DataGridBoundColumn)
                .OrderBy(c => c.DisplayIndex)
                .FirstOrDefault();

            if (primeiraColunaDados is null)
            {
                return;
            }

            ResultDataGrid.CurrentCell = new DataGridCellInfo(item, primeiraColunaDados);
            ResultDataGrid.Focus();
        }

        private void SelecionarColuna(DataGridColumn coluna, ModifierKeys modificadores)
        {
            var indiceColuna = coluna.DisplayIndex;

            if (_tipoSelecaoAtual == TipoSelecaoAtual.Linha)
            {
                LimparSelecao();
            }

            _tipoSelecaoAtual = TipoSelecaoAtual.Coluna;

            if (!modificadores.HasFlag(ModifierKeys.Control) && !modificadores.HasFlag(ModifierKeys.Shift))
            {
                LimparSelecao();
                SelecionarColunaCompleta(coluna);
                _ultimoIndiceColunaSelecionada = indiceColuna;
                return;
            }

            if (modificadores.HasFlag(ModifierKeys.Shift) && _ultimoIndiceColunaSelecionada.HasValue)
            {
                var inicio = Math.Min(_ultimoIndiceColunaSelecionada.Value, indiceColuna);
                var fim = Math.Max(_ultimoIndiceColunaSelecionada.Value, indiceColuna);
                var colunasNaFaixa = ResultDataGrid.Columns
                    .Where(c => c.DisplayIndex >= inicio && c.DisplayIndex <= fim && c.Visibility == Visibility.Visible)
                    .OrderBy(c => c.DisplayIndex)
                    .ToList();

                foreach (var colunaFaixa in colunasNaFaixa)
                {
                    SelecionarColunaCompleta(colunaFaixa);
                }
            }
            else
            {
                SelecionarColunaCompleta(coluna);
            }

            _ultimoIndiceColunaSelecionada = indiceColuna;
        }

        private void SelecionarColunaCompleta(DataGridColumn coluna)
        {
            foreach (var item in ResultDataGrid.Items)
            {
                if (item == CollectionView.NewItemPlaceholder)
                {
                    continue;
                }

                var celula = new DataGridCellInfo(item, coluna);
                if (!ResultDataGrid.SelectedCells.Contains(celula))
                {
                    ResultDataGrid.SelectedCells.Add(celula);
                }
            }
        }

        private void SelecionarLinhaCompleta(object item)
        {
            if (!ResultDataGrid.SelectedItems.Contains(item))
            {
                ResultDataGrid.SelectedItems.Add(item);
            }

            foreach (var coluna in ResultDataGrid.Columns.Where(c => c.Visibility == Visibility.Visible && c is DataGridBoundColumn))
            {
                var celula = new DataGridCellInfo(item, coluna);
                if (!ResultDataGrid.SelectedCells.Contains(celula))
                {
                    ResultDataGrid.SelectedCells.Add(celula);
                }
            }
        }

        private object? ObterItemPorIndice(int indice)
        {
            if (indice < 0 || indice >= ResultDataGrid.Items.Count)
            {
                return null;
            }

            var item = ResultDataGrid.Items[indice];
            return item == CollectionView.NewItemPlaceholder ? null : item;
        }

        private void LimparSelecao()
        {
            ResultDataGrid.SelectedItems.Clear();
            ResultDataGrid.SelectedCells.Clear();
        }

        private void OrdenarPorColuna(DataGridColumn coluna)
        {
            if (ResultDataGrid.ItemsSource is null)
            {
                return;
            }

            if (coluna is not DataGridBoundColumn colunaVinculada || colunaVinculada.Binding is not Binding { Path.Path: { } caminho })
            {
                return;
            }

            var direcao = coluna.SortDirection == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;

            foreach (var colunaGrid in ResultDataGrid.Columns)
            {
                colunaGrid.SortDirection = null;
            }

            var view = CollectionViewSource.GetDefaultView(ResultDataGrid.ItemsSource);
            view.SortDescriptions.Clear();
            view.SortDescriptions.Add(new SortDescription(caminho, direcao));
            view.Refresh();

            coluna.SortDirection = direcao;
        }

        private static T? ObterAncestor<T>(DependencyObject? objeto) where T : DependencyObject
        {
            while (objeto is not null)
            {
                if (objeto is T ancestor)
                {
                    return ancestor;
                }

                objeto = VisualTreeHelper.GetParent(objeto);
            }

            return null;
        }

        private void RowNumberHeaderMenuButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button botao)
            {
                return;
            }

            var menu = new ContextMenu();

            var copiarMenuItem = new MenuItem { Header = "Copiar todos os dados (Ctrl+C)" };
            copiarMenuItem.Click += (_, _) => CopiarTodosDados();
            menu.Items.Add(copiarMenuItem);

            botao.ContextMenu = menu;
            menu.PlacementTarget = botao;
            menu.Placement = PlacementMode.Bottom;
            menu.IsOpen = true;
        }

        private void CopiarTodosDados()
        {
            if (_ultimoResultado.Count == 0)
            {
                MessageBox.Show(this, "Não há dados para copiar.", "Informação", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var colunasVisiveis = ResultDataGrid.Columns
                .Where(coluna => coluna.Visibility == Visibility.Visible)
                .OrderBy(coluna => coluna.DisplayIndex)
                .OfType<DataGridBoundColumn>()
                .Select(coluna => new
                {
                    Cabecalho = coluna.Header?.ToString() ?? string.Empty,
                    NomePropriedade = (coluna.Binding as Binding)?.Path?.Path ?? string.Empty
                })
                .Where(coluna => !string.IsNullOrWhiteSpace(coluna.NomePropriedade))
                .ToList();

            if (colunasVisiveis.Count == 0)
            {
                MessageBox.Show(this, "Não há colunas visíveis para copiar.", "Informação", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var conteudo = new StringBuilder();
            conteudo.AppendLine(string.Join('\t', colunasVisiveis.Select(coluna => coluna.Cabecalho)));

            foreach (var registro in _ultimoResultado)
            {
                var valores = colunasVisiveis
                    .Select(coluna => SanitizarTextoParaClipboard(ObterValorColuna(registro, coluna.NomePropriedade)));
                conteudo.AppendLine(string.Join('\t', valores));
            }

            Clipboard.SetText(conteudo.ToString());
        }

        private static string SanitizarTextoParaClipboard(string valor)
        {
            return valor
                .Replace('\t', ' ')
                .Replace('\r', ' ')
                .Replace('\n', ' ');
        }

        private static string ObterValorColuna(RegistroAnalise registro, string nomePropriedade)
        {
            if (MapeamentoColunas.TryGetValue(nomePropriedade, out var seletorValor))
            {
                return seletorValor(registro) ?? string.Empty;
            }

            var propriedade = registro.GetType().GetProperty(nomePropriedade, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            var valor = propriedade?.GetValue(registro);
            return valor?.ToString() ?? string.Empty;
        }

    }
}