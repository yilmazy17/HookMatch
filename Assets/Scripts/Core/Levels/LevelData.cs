using System;
using System.Collections.Generic;

namespace Core
{
    [Serializable]
    public class LevelData
    {
        public int level_number;
        public int grid_width;
        public int grid_height;
        public int move_count;
        public string[] grid;

        // JsonUtility can't deserialize a dynamic-keyed JSON object (target_cubes)
        // into a Dictionary, so this is populated separately by LevelManager after
        // parsing. Levels that don't have the field yet just get an empty dictionary.
        [NonSerialized] public Dictionary<string, int> target_cubes = new Dictionary<string, int>();
    }
}
