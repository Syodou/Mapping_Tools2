using System.Collections.Generic;

namespace Mapping_Tools.Classes.Tools.HitsoundLibraryStudio {

    public enum ConflictType {
        /// <summary>Dos SoundEntries apuntan al mismo nombre de archivo osu!</summary>
        DuplicateTarget,
        /// <summary>El nombre de archivo ya existe en la carpeta del mapa</summary>
        ExistsInBeatmapFolder,
        /// <summary>El rango de números superpone al de otra entrada</summary>
        RangeOverlap
    }

    /// <summary>Describe un conflicto detectado en la librería de sonidos</summary>
    public class ConflictResult {
        public ConflictType Type { get; init; }
        public string Message { get; init; }
        public List<string> InvolvedTargets { get; init; } = new();
        public List<string> InvolvedEntries { get; init; } = new();

        public override string ToString() => $"[{Type}] {Message}";
    }
}
