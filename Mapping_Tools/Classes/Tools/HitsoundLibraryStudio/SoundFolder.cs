using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

namespace Mapping_Tools.Classes.Tools.HitsoundLibraryStudio {

    /// <summary>
    /// Carpeta/categoría que agrupa SoundEntries para mejor organización.
    /// Ejemplo: "Drums", "SFX de combate", "Ambient"
    /// </summary>
    public class SoundFolder : INotifyPropertyChanged {

        private string _name = "Nueva Carpeta";
        private bool _isExpanded = true;

        public string Name {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public bool IsExpanded {
            get => _isExpanded;
            set { _isExpanded = value; OnPropertyChanged(); }
        }

        /// <summary>Entradas de sonido dentro de esta carpeta</summary>
        public ObservableCollection<SoundEntry> Entries { get; set; } = new();

        [JsonIgnore]
        public string DisplayName =>
            string.IsNullOrWhiteSpace(Name) ? "(Sin nombre)" : $"📁 {Name} ({Entries.Count})";

        public override string ToString() => DisplayName;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
