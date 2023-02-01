using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Grid_System : MonoBehaviour
{
    private static Grid_System instance;
    private static int MAP_SIZE = 100;
    private static int MAP_START_X = -50;
    private static int MAP_START_Z = -50;

    private bool[,] grid = new bool[MAP_SIZE, MAP_SIZE];

    public static Grid_System GetInstance()
    {
        return instance;
    }

    private void Start()
    {
        instance = this;
    }

    public bool IsWalkable(int x, int z)
    {
        return !grid[x - MAP_START_X, z - MAP_START_Z];
    }
}
