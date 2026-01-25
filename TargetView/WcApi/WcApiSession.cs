using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game.Components;
using VRage.Game.Entity;

namespace TargetView.WcApi;

[MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
internal class WcApiSession : MySessionComponentBase
{
    private static CoreSystems.Api.WcApi? _api;
    private static bool _apiReady;

    public override void LoadData()
    {
        _api = new CoreSystems.Api.WcApi();
        _api.Load(() => _apiReady = true, false);
    }

    protected override void UnloadData()
    {
        _api?.Unload();
        _api = null;
        _apiReady = false;
    }

    public static MyEntity? GetLockedTarget(MyEntity? shooter)
    {
        if (shooter is null || _api is null || !_apiReady)
        {
            return null;
        }

        return _api.GetAiFocus(shooter);
    }
}
