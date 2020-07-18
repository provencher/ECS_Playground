using System.Diagnostics;
using Fire;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace Water
{
    public class RaycastSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var mouseLeftDown = UnityEngine.Input.GetMouseButton(0);
            var mouseRightDown = UnityEngine.Input.GetMouseButton(1);

            if (!mouseLeftDown && !mouseRightDown)
                return;

            var camera = UnityEngine.Camera.main;
            if (camera == null)
                return;

            var ray = camera.ScreenPointToRay(UnityEngine.Input.mousePosition);
            new UnityEngine.Plane(UnityEngine.Vector3.up, 0).Raycast(ray, out var enter);
            var hit = (float3)ray.GetPoint(enter);

            const float splashRadius = 1f;

            Entities
                .ForEach((Entity entity, int entityInQueryIndex, ref TemperatureComponent temperature, in Translation trans, in WorldRenderBounds bounds, in LocalToWorld ltw) =>
            {
                float distance = math.distancesq(ltw.Position, hit);
                if (math.distancesq(ltw.Position, hit) < splashRadius)
                {
                    float affect = distance / bounds.Value.Extents.x;
                    if (mouseLeftDown)
                    {
                        temperature.Value += 0.5f * affect;
                    }
                    else if (mouseRightDown)
                    {
                        temperature.Value -= 0.5f * affect;
                    }
                }
            }).ScheduleParallel();

        }
    }
}
