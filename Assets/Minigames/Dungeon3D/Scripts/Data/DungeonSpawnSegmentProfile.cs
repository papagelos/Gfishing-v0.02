using System;
using System.Collections.Generic;
using UnityEngine;

namespace GalacticFishing.Minigames.Dungeon3D
{
    [Serializable]
    public sealed class SpawnSegment
    {
        [Min(0)] public int segmentStartSec = 0;
        [Min(0)] public int enemyMinimum = 20;
        [Min(0f)] public float quotaMultiplier = 1f;
        [Min(0)] public int maintenanceSpawnCount = 1;
    }

    [CreateAssetMenu(
        menuName = "Galactic Fishing/Dungeon/Spawn Segment Profile",
        fileName = "DungeonSpawnSegmentProfile_")]
    public sealed class DungeonSpawnSegmentProfile : ScriptableObject
    {
        [Header("Director Limits")]
        [Min(1)] public int globalAliveCap = 300;
        [Min(1)] public int spawnBudgetPerFrame = 5;

        [Header("Segment Table")]
        public List<SpawnSegment> segments = new()
        {
            new SpawnSegment
            {
                segmentStartSec = 0,
                enemyMinimum = 20,
                quotaMultiplier = 1f,
                maintenanceSpawnCount = 1
            },
            new SpawnSegment
            {
                segmentStartSec = 60,
                enemyMinimum = 30,
                quotaMultiplier = 1.2f,
                maintenanceSpawnCount = 2
            }
        };

        public bool TryGetSegmentForElapsedSeconds(int elapsedSeconds, out SpawnSegment segment)
        {
            segment = null;
            if (segments == null || segments.Count == 0)
                return false;

            int clampedElapsed = Mathf.Max(0, elapsedSeconds);
            SpawnSegment best = null;
            int bestStart = int.MinValue;

            for (int i = 0; i < segments.Count; i++)
            {
                SpawnSegment candidate = segments[i];
                if (candidate == null)
                    continue;

                int start = Mathf.Max(0, candidate.segmentStartSec);
                if (start > clampedElapsed || start < bestStart)
                    continue;

                best = candidate;
                bestStart = start;
            }

            if (best == null)
                best = segments[0];

            segment = best;
            return segment != null;
        }

        private void OnValidate()
        {
            globalAliveCap = Mathf.Max(1, globalAliveCap);
            spawnBudgetPerFrame = Mathf.Max(1, spawnBudgetPerFrame);

            if (segments == null)
                segments = new List<SpawnSegment>();

            for (int i = 0; i < segments.Count; i++)
            {
                SpawnSegment segment = segments[i];
                if (segment == null)
                    continue;

                segment.segmentStartSec = Mathf.Max(0, segment.segmentStartSec);
                segment.enemyMinimum = Mathf.Max(0, segment.enemyMinimum);
                segment.quotaMultiplier = Mathf.Max(0f, segment.quotaMultiplier);
                segment.maintenanceSpawnCount = Mathf.Max(0, segment.maintenanceSpawnCount);
            }
        }
    }
}
