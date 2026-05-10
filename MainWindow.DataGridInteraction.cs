using System.ComponentModel;
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

namespace AnaliseDemonstrativoTISS;

public partial class MainWindow
{
    private TipoSelecaoAtual _tipoSelecaoAtual = TipoSelecaoAtual.Nenhuma;
    private int? _ultimoIndiceLinhaSelecionada;
    private int? _ultimoIndiceColunaSelecionada;

    private enum TipoSelecaoAtual
    {
        Nenhuma,
        Linha,
        Coluna
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

        if (ObterAncestor<Thumb>(source) is not null)
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
