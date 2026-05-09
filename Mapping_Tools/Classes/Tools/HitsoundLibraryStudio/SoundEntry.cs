using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

namespace Mapping_Tools.Classes.Tools.HitsoundLibraryStudio {

    /// <summary>
    /// Representa un sonido en la librería con su nombre personalizado
    /// y su(s) nombre(s) de destino en formato osu!
    /// </summary>
    public class SoundEntry : INotifyPropertyChanged {

        // ── Campos privados ──────────────────────────────────────────────────

        private string _customName = "Nuevo Sonido";
        private string _sourceFilePath = string.Empty;
        private string _primaryTarget = string.Empty;   // ej: soft-hitwhistle12
        private bool _useRange;
        private int _rangeStart = 1;
        private int _rangeEnd = 1;
        private int _volume = 100;
        private string _notes = string.Empty;

        // ── Propiedades ──────────────────────────────────────────────────────

        /// <summary>Nombre legible para el mapper (ej: "Kick", "Disparo")</summary>
        public string CustomName {
            get => _customName;
            set { _customName = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
        }

        /// <summary>Ruta al archivo de audio fuente (.wav / .ogg / .mp3)</summary>
        public string SourceFilePath {
            get => _sourceFilePath;
            set { _sourceFilePath = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasSource)); }
        }

        /// <summary>
        /// Nombre base de hitsound en osu! (ej: "soft-hitwhistle12").
        /// Si UseRange=true, el número al final se reemplaza por el rango RangeStart..RangeEnd.
        /// </summary>
        public string PrimaryTarget {
            get => _primaryTarget;
            set { _primaryTarget = value; OnPropertyChanged(); OnPropertyChanged(nameof(ResolvedTargets)); }
        }

        /// <summary>Si es true, genera múltiples archivos con un rango numérico</summary>
        public bool UseRange {
            get => _useRange;
            set { _useRange = value; OnPropertyChanged(); OnPropertyChanged(nameof(ResolvedTargets)); }
        }

        /// <summary>Número inicial del rango (solo relevante cuando UseRange=true)</summary>
        public int RangeStart {
            get => _rangeStart;
            set { _rangeStart = value; OnPropertyChanged(); OnPropertyChanged(nameof(ResolvedTargets)); }
        }

        /// <summary>Número final del rango (solo relevante cuando UseRange=true)</summary>
        public int RangeEnd {
            get => _rangeEnd;
            set { _rangeEnd = value; OnPropertyChanged(); OnPropertyChanged(nameof(ResolvedTargets)); }
        }

        /// <summary>Volumen de importación (0–100)</summary>
        public int Volume {
            get => _volume;
            set { _volume = Math.Clamp(value, 0, 100); OnPropertyChanged(); }
        }

        /// <summary>Notas adicionales del mapper</summary>
        public string Notes {
            get => _notes;
            set { _notes = value; OnPropertyChanged(); }
        }

        // ── Propiedades calculadas (no serializadas) ─────────────────────────

        [JsonIgnore]
        public bool HasSource => !string.IsNullOrWhiteSpace(SourceFilePath);

        [JsonIgnore]
        public string DisplayName =>
            string.IsNullOrWhiteSpace(CustomName) ? "(Sin nombre)" : CustomName;

        /// <summary>
        /// Devuelve la lista de nombres de archivo osu! que se generarán al exportar.
        /// Ejemplo con rango: drum-hitnormal12, drum-hitnormal13, ..., drum-hitnormal20
        /// </summary>
        [JsonIgnore]
        public IReadOnlyList<string> ResolvedTargets {
            get {
                if (string.IsNullOrWhiteSpace(PrimaryTarget))
                    return Array.Empty<string>();

                if (!UseRange)
                    return new[] { PrimaryTarget };

                // Extraer base sin el número final, si lo tiene
                string baseName = StripTrailingNumber(PrimaryTarget);

                var list = new List<string>();
                int start = Math.Min(RangeStart, RangeEnd);
                int end   = Math.Max(RangeStart, RangeEnd);
                for (int i = start; i <= end; i++)
                    list.Add($"{baseName}{i}");
                return list;
            }
        }

        // ── Utilidades ───────────────────────────────────────────────────────

        /// <summary>Quita los dígitos finales de un string (ej: "drum-hitnormal12" → "drum-hitnormal")</summary>
        public static string StripTrailingNumber(string s) {
            int i = s.Length - 1;
            while (i >= 0 && char.IsDigit(s[i])) i--;
            return s[..( i + 1)];
        }

        public override string ToString() => DisplayName;

        // ── INotifyPropertyChanged ───────────────────────────────────────────

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
