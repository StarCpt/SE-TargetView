using Sandbox.Game.Entities;
using System.Collections.Generic;

namespace TargetView;

public static class Utils
{
    public static List<MyCameraBlock> GetAllCameraBlocks(this MyCubeGrid grid)
    {
        return grid.GridSystems.CameraSystem.m_cameras;
    }
}
