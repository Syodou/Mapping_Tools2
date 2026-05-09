using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Mapping_Tools.Classes.SystemTools;
using Mapping_Tools.Classes.SystemTools.QuickRun;
using Mapping_Tools.Classes.Tools;
using Mapping_Tools.Classes.Tools.HitsoundLibraryStudio;
using Mapping_Tools.Viewmodels;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace Mapping_Tools.Views.HitsoundLibraryStudio {

    /// <summary>
    /// Hitsound Library Studio – gestiona una librería de sonidos con nombres
    /// personalizados y los exporta al formato osu! correcto.
    /// </summary>
    [SmartQuickRunUsage(SmartQuickRunTargets.AnySelection)]
    public partial class HitsoundLibraryView : ISavable<HitsoundLibraryVm> {

        // ────────────────────────────────────────────────────────────────────
        //  Constantes de la herramienta
        // ────────────────────────────────────────────────────────────────────

        public static readonly string ToolName = "Hitsound Library Studio";
        public static readonly string ToolDescription =
            "Gestiona una librería de hitsounds con nombres personalizados, " +
            "organizada por carpetas. Exporta los sonidos al formato osu! y " +
            "los copia directamente a la carpeta del mapa.";

        // ────────────────────────────────────────────────────────────────────
        //  Constructor
        // ────────────────────────────────────────────────────────────────────

        public HitsoundLibraryView() {
            InitializeComponent();
            Width  = MainWindow.AppWindow.content_views.Width;
            Height = MainWindow.AppWindow.content_views.Height;
            DataContext = new HitsoundLibraryVm();
            ProjectManager.LoadProject(this, message: false);
        }

        public HitsoundLibraryVm ViewModel => (HitsoundLibraryVm) DataContext;

        // ────────────────────────────────────────────────────────────────────
        //  ISavable<HitsoundLibraryVm>
        // ────────────────────────────────────────────────────────────────────

        public HitsoundLibraryVm GetSaveData() => ViewModel;

        public void SetSaveData(HitsoundLibraryVm saveData) => DataContext = saveData;

        public string AutoSavePath =>
            Path.Combine(MainWindow.AppDataPath, "hitsoundlibraryproject.json");

        public string DefaultSaveFolder =>
            Path.Combine(MainWindow.AppDataPath, "Hitsound Library Projects");

        // ────────────────────────────────────────────────────────────────────
        //  BackgroundWorker – lógica principal de exportación
        // ────────────────────────────────────────────────────────────────────

        protected override void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e) {
            var bgw = sender as BackgroundWorker;
            e.Result = RunExport((HitsoundLibraryVm) e.Argument, bgw);
        }

        private string RunExport(HitsoundLibraryVm vm, BackgroundWorker worker) {
            // ── Validaciones ─────────────────────────────────────────────────

            if (!vm.HasBeatmap)
                throw new InvalidOperationException("No se ha seleccionado un archivo .osu válido.");

            string beatmapFolder = vm.BeatmapFolderPath;
            if (!Directory.Exists(beatmapFolder))
                throw new DirectoryNotFoundException($"La carpeta del mapa no existe: {beatmapFolder}");

            var allEntries = vm.AllEntries.ToList();
            if (allEntries.Count == 0)
                throw new InvalidOperationException("La librería está vacía. Agrega sonidos antes de exportar.");

            // ── Detección de conflictos (pre-export) ─────────────────────────

            var conflicts = vm.DetectConflicts();
            var blocking = conflicts.Where(c => c.Type == ConflictType.DuplicateTarget ||
                                                 c.Type == ConflictType.RangeOverlap).ToList();

            if (blocking.Count > 0 && !vm.SkipConflicts) {
                var sb = new StringBuilder();
                sb.AppendLine($"Se encontraron {blocking.Count} conflicto(s) bloqueantes:");
                foreach (var c in blocking) sb.AppendLine($"  • {c.Message}");
                sb.AppendLine("\nActiva 'Ignorar conflictos' en la configuración para exportar de todas formas.");
                throw new InvalidOperationException(sb.ToString());
            }

            // ── Backup de archivos existentes ────────────────────────────────

            if (vm.BackupExisting) {
                string backupFolder = Path.Combine(beatmapFolder, "_hs_backup");
                Directory.CreateDirectory(backupFolder);

                foreach (var entry in allEntries) {
                    foreach (var target in entry.ResolvedTargets) {
                        foreach (var ext in new[] { ".wav", ".ogg", ".mp3" }) {
                            string existing = Path.Combine(beatmapFolder, target + ext);
                            if (File.Exists(existing)) {
                                string dest = Path.Combine(backupFolder, target + ext);
                                File.Copy(existing, dest, overwrite: true);
                            }
                        }
                    }
                }
            }

            // ── Exportación ──────────────────────────────────────────────────

            int exported = 0;
            int total    = allEntries.Sum(e => e.ResolvedTargets.Count);
            int done     = 0;

            foreach (var entry in allEntries) {
                if (string.IsNullOrWhiteSpace(entry.SourceFilePath) || !File.Exists(entry.SourceFilePath)) {
                    // Sin fuente: saltar (no es error fatal)
                    done += entry.ResolvedTargets.Count;
                    continue;
                }

                string sourceExt = Path.GetExtension(entry.SourceFilePath).ToLowerInvariant();

                foreach (var target in entry.ResolvedTargets) {
                    string destFile = Path.Combine(beatmapFolder, target + sourceExt);

                    if (File.Exists(destFile) && !vm.OverwriteExisting) {
                        done++;
                        continue;
                    }

                    File.Copy(entry.SourceFilePath, destFile, overwrite: true);
                    exported++;
                    done++;

                    if (worker?.WorkerReportsProgress == true)
                        worker.ReportProgress(done * 100 / Math.Max(total, 1));
                }
            }

            if (worker?.WorkerReportsProgress == true) worker.ReportProgress(100);

            return $"¡Exportación completada! {exported} archivo(s) copiado(s) a la carpeta del mapa.";
        }

        // ────────────────────────────────────────────────────────────────────
        //  Botón principal: Exportar
        // ────────────────────────────────────────────────────────────────────

        private void ExportButton_Click(object sender, RoutedEventArgs e) {
            if (!CanRun) return;

            FocusManager.SetFocusedElement(FocusManager.GetFocusScope(this), null);

            if (!ViewModel.HasBeatmap) {
                MessageBox.Show("Selecciona un archivo .osu antes de exportar.",
                    ToolName, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BackupManager.SaveMapBackup(new[] { ViewModel.BeatmapPath });
            ViewModel.Paths = new[] { ViewModel.BeatmapPath };
            ViewModel.Quick = false;

            BackgroundWorker.RunWorkerAsync(ViewModel);
            CanRun = false;
        }

        // ────────────────────────────────────────────────────────────────────
        //  Botón: Detectar conflictos
        // ────────────────────────────────────────────────────────────────────

        private void CheckConflictsButton_Click(object sender, RoutedEventArgs e) {
            var conflicts = ViewModel.DetectConflicts();

            if (conflicts.Count == 0) {
                MessageBox.Show("✅ No se encontraron conflictos en la librería.",
                    "Verificación de Conflictos", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Se encontraron {conflicts.Count} conflicto(s):\n");

            foreach (var group in conflicts.GroupBy(c => c.Type)) {
                sb.AppendLine($"【{DescribeConflictType(group.Key)}】");
                foreach (var c in group)
                    sb.AppendLine($"  • {c.Message}");
                sb.AppendLine();
            }

            // Mostrar en ventana scrollable
            var win = new Window {
                Title = "Conflictos detectados",
                Width = 640, Height = 420,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = MainWindow.AppWindow,
                Content = new ScrollViewer {
                    Margin = new Thickness(12),
                    Content = new TextBlock {
                        Text = sb.ToString(),
                        FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            };
            win.ShowDialog();
        }

        private static string DescribeConflictType(ConflictType t) => t switch {
            ConflictType.DuplicateTarget    => "Targets duplicados",
            ConflictType.RangeOverlap       => "Rangos superpuestos",
            ConflictType.ExistsInBeatmapFolder => "Archivos ya existentes en el mapa",
            _ => t.ToString()
        };

        // ────────────────────────────────────────────────────────────────────
        //  Gestión de carpetas
        // ────────────────────────────────────────────────────────────────────

        private void AddFolderButton_Click(object sender, RoutedEventArgs e) {
            var folder = new SoundFolder { Name = "Nueva Carpeta" };
            ViewModel.Folders.Add(folder);
        }

        private void RemoveFolderButton_Click(object sender, RoutedEventArgs e) {
            if (FoldersList.SelectedItem is SoundFolder folder) {
                var result = MessageBox.Show(
                    $"¿Eliminar la carpeta \"{folder.Name}\" y sus {folder.Entries.Count} entrada(s)?",
                    ToolName, MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                    ViewModel.Folders.Remove(folder);
            }
        }

        // ────────────────────────────────────────────────────────────────────
        //  Gestión de entradas de sonido
        // ────────────────────────────────────────────────────────────────────

        private void AddEntryButton_Click(object sender, RoutedEventArgs e) {
            if (FoldersList.SelectedItem is not SoundFolder folder) {
                MessageBox.Show("Selecciona una carpeta primero.", ToolName,
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var entry = new SoundEntry { CustomName = "Nuevo Sonido" };
            folder.Entries.Add(entry);
            EntriesList.SelectedItem = entry;
        }

        private void RemoveEntryButton_Click(object sender, RoutedEventArgs e) {
            if (FoldersList.SelectedItem is not SoundFolder folder) return;
            if (EntriesList.SelectedItem is not SoundEntry entry) return;

            folder.Entries.Remove(entry);
        }

        /// <summary>Duplica una entrada y asigna automáticamente el siguiente número libre en el rango</summary>
        private void DuplicateEntryButton_Click(object sender, RoutedEventArgs e) {
            if (FoldersList.SelectedItem is not SoundFolder folder) return;
            if (EntriesList.SelectedItem is not SoundEntry source) return;

            // Serializar + deserializar para clonar profundamente
            string json   = JsonConvert.SerializeObject(source);
            var    clone  = JsonConvert.DeserializeObject<SoundEntry>(json);
            clone.CustomName = source.CustomName + " (copia)";

            // Si usa rango, avanzar el rango para no duplicar
            if (clone.UseRange) {
                int offset = clone.RangeEnd - clone.RangeStart + 1;
                clone.RangeStart = source.RangeEnd + 1;
                clone.RangeEnd   = source.RangeEnd + offset;
            }

            int idx = folder.Entries.IndexOf(source);
            folder.Entries.Insert(idx + 1, clone);
            EntriesList.SelectedItem = clone;
        }

        // ────────────────────────────────────────────────────────────────────
        //  Selector de archivo fuente
        // ────────────────────────────────────────────────────────────────────

        private void BrowseSourceButton_Click(object sender, RoutedEventArgs e) {
            if (EntriesList.SelectedItem is not SoundEntry entry) return;

            var dlg = new OpenFileDialog {
                Title  = "Seleccionar archivo de audio",
                Filter = "Archivos de audio|*.wav;*.ogg;*.mp3|Todos|*.*",
                CheckFileExists = true
            };

            if (dlg.ShowDialog() == true)
                entry.SourceFilePath = dlg.FileName;
        }

        private void BrowseBeatmapButton_Click(object sender, RoutedEventArgs e) {
            var dlg = new OpenFileDialog {
                Title  = "Seleccionar archivo .osu",
                Filter = "osu! Beatmap|*.osu|Todos|*.*",
                CheckFileExists = true
            };
            if (dlg.ShowDialog() == true)
                ViewModel.BeatmapPath = dlg.FileName;
        }

        private void UseCurrentBeatmapButton_Click(object sender, RoutedEventArgs e) {
            string path = IOHelper.GetCurrentBeatmapOrCurrentBeatmap();
            if (!string.IsNullOrWhiteSpace(path))
                ViewModel.BeatmapPath = path;
        }

        // ────────────────────────────────────────────────────────────────────
        //  Preview de targets resueltos
        // ────────────────────────────────────────────────────────────────────

        private void PreviewTargetsButton_Click(object sender, RoutedEventArgs e) {
            if (EntriesList.SelectedItem is not SoundEntry entry) return;

            var targets = entry.ResolvedTargets;
            if (targets.Count == 0) {
                MessageBox.Show("Esta entrada no tiene targets definidos.", ToolName,
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string preview = string.Join("\n", targets.Select((t, i) => $"  {i + 1}. {t}"));
            MessageBox.Show($"Targets generados ({targets.Count}):\n\n{preview}", 
                "Preview de Targets", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
