using System.Collections.Generic;

namespace CreatorMap.Scripts.Data
{
    /// <summary>
    /// Extensions methods for Cell data structures
    /// </summary>
    public static class CellExtensions
    {
        /// <summary>
        /// Rebuilds a dictionary from the list of cells.
        /// This is used when importing cell data to ensure the dictionary representation is properly updated.
        /// </summary>
        public static void RebuildDictionary(this List<Cell> cells)
        {
            if (cells == null)
                return;
                
            // This method rebuilds any necessary in-memory structures from the cells list
            // Since we're migrating from a dictionary-based structure to a list-based one,
            // additional logic may be needed to ensure compatibility with older systems
            
            // If any specific rebuilding logic is required, it would go here
            // For example, if there's a secondary index or lookup structure we need to maintain
        }
    }
} 