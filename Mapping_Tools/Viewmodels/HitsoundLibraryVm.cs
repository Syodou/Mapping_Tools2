using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Mapping_Tools.Classes.Tools.HitsoundLibraryStudio;
using Newtonsoft.Json;

namespace Mapping_Tools.Viewmodels {

    public class HitsoundLibraryVm : INotifyPropertyChanged {

        // ── Ruta del .osu ────────────────────────────────────────────────────

        private string _beatmapPath = string.Empty;

        [JsonProperty]
        public string BeatmapPath {
            get => _beatmapPath;
            set {
                _beatmapPath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BeatmapFolderPath));
                OnPropertyChanged(nameof(HasBeatmap));
            }
        }

        [JsonIgnore]
        public bool HasBeatmap => File.Exists(BeatmapPath);

        [JsonIgnore]
        public string BeatmapFolderPath =>
            HasBeatmap ? Path.GetDirectoryName(BeatmapPath) : string.Empty;

        // ── Librería de sonidos ──────────────────────────────────────────────

        [JsonProperty]
        public ObservableCollection<SoundFolder> Folders { get; set; } = new();

        // ── Configuración de exportación ─────────────────────────────────────

        private bool _overwriteExisting = true;
        private bool _backupExisting = true;
        private bool _skipConflicts;

        [JsonProperty]
        public bool OverwriteExisting {
            get => _overwriteExisting;
            set { _overwriteExisting = value; OnPropertyChanged(); }
        }

        [JsonProperty]
        public bool BackupExisting {
            get => _backupExisting;
            set { _backupExisting = value; OnPropertyChanged(); }
        }

        /// <summary>Si hay conflictos de duplicados, saltarlos en lugar de fallar</summary>
        [JsonProperty]
        public bool SkipConflicts {
            get => _skipConflicts;
            set { _skipConflicts = value; OnPropertyChanged(); }
        }

        // ── Paths (para BackgroundWorker) ────────────────────────────────────

        [JsonIgnore]
        public string[] Paths { get; set; }

        [JsonIgnore]
        public bool Quick { get; set; }

        // ── Helpers ──────────────────────────────────────────────────────────

        /// <summary>Devuelve todas las SoundEntries a lo largo de todas las carpetas</summary>
        [JsonIgnore]
        public IEnumerable<SoundEntry> AllEntries =>
            Folders.SelectMany(f => f.Entries);

        /// <summary>
        /// Detecta conflictos en la librería actual:
        /// 1. Targets duplicados entre entradas
        /// 2. Rangos superpuestos entre entradas
        /// 3. Archivos ya existentes en la carpeta del mapa (si se conoce)
        /// </summary>
        public List<ConflictResult> DetectConflicts() {
            var results = new List<ConflictResult>();
            var allEntries = AllEntries.ToList();

            // Mapa de target → lista de nombres de entradas que lo usan
            var targetUsage = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in allEntries) {
                foreach (var target in entry.ResolvedTargets) {
                    if (!targetUsage.ContainsKey(target))
                        targetUsage[target] = new List<string>();
                    targetUsage[target].Add(entry.CustomName);
                }
            }

            // 1. Targets duplicados
            foreach (var (target, users) in targetUsage) {
                if (users.Count > 1) {
                    results.Add(new ConflictResult {
                        Type = ConflictType.DuplicateTarget,
                        Message = $"El target \"{target}\" es usado por {users.Count} entradas: {string.Join(", ", users)}",
                        InvolvedTargets = new List<string> { target },
                        InvolvedEntries = users
                    });
                }
            }

            // 2. Rangos superpuestos entre entradas con UseRange=true que comparten base
            var rangedEntries = allEntries.Where(e => e.UseRange).ToList();
            for (int i = 0; i < rangedEntries.Count; i++) {
                for (int j = i + 1; j < rangedEntries.Count; j++) {
                    var a = rangedEntries[i];
                    var b = rangedEntries[j];

                    string baseA = SoundEntry.StripTrailingNumber(a.PrimaryTarget);
                    string baseB = SoundEntry.StripTrailingNumber(b.PrimaryTarget);

                    if (!string.Equals(baseA, baseB, StringComparison.OrdinalIgnoreCase)) continue;

                    int aStart = Math.Min(a.RangeStart, a.RangeEnd);
                    int aEnd   = Math.Max(a.RangeStart, a.RangeEnd);
                    int bStart = Math.Min(b.RangeStart, b.RangeEnd);
                    int bEnd   = Math.Max(b.RangeStart, b.RangeEnd);

                    bool overlaps = aStart <= bEnd && bStart <= aEnd;
                    if (overlaps) {
                        results.Add(new ConflictResult {
                            Type = ConflictType.RangeOverlap,
                            Message = $"Los rangos de \"{a.CustomName}\" ({aStart}–{aEnd}) y \"{b.CustomName}\" ({bStart}–{bEnd}) se superponen en base \"{baseA}\"",
                            InvolvedEntries = new List<string> { a.CustomName, b.CustomName }
                        });
                    }
                }
            }

            // 3. Archivos ya existentes en la carpeta del mapa
            if (!string.IsNullOrWhiteSpace(BeatmapFolderPath) && Directory.Exists(BeatmapFolderPath)) {
                foreach (var (target, users) in targetUsage) {
                    // Buscar cualquier extensión posible
                    foreach (var ext in new[] { ".wav", ".ogg", ".mp3" }) {
                        string candidate = Path.Combine(BeatmapFolderPath, target + ext);
                        if (File.Exists(candidate)) {
                            results.Add(new ConflictResult {
                                Type = ConflictType.ExistsInBeatmapFolder,
                                Message = $"Ya existe \"{target + ext}\" en la carpeta del mapa (usado por: {string.Join(", ", users)})",
                                InvolvedTargets = new List<string> { target + ext },
                                InvolvedEntries = users
                            });
                            break; // solo reportar una vez por target
                        }
                    }
                }
            }

            return results;
        }
    }
}
