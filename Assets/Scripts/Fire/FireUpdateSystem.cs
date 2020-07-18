using System;
using System.Data.SqlTypes;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Utils;
using Water;
using Random = Unity.Mathematics.Random;

namespace Fire
{
    public class FireUpdateSystem : SystemBase
    {
        [BurstCompile]
        struct CopyTemperaturesFromFireGrid : IJob
        {
            [ReadOnly]
            public ComponentDataFromEntity<TemperatureComponent> temperatures;
            public NativeArray<FireBufferElement> gridArray;
            public NativeArray<TemperatureComponent> temperatureComponents;

            public void Execute()
            {
                for (int i = 0; i < gridArray.Length; i++)
                {
                    temperatureComponents[i] = temperatures[gridArray[i].FireEntity];
                }
            }
        }

        protected override void OnUpdate()
        {
            // Check if we have initialized
            EntityQuery queryGroup = GetEntityQuery(typeof(Initialized));
            if (queryGroup.CalculateEntityCount() == 0)
            {
                return;
            }

            float deltaTime = Time.DeltaTime;
            float epsilon = Mathf.Epsilon;
            float elapsedTime = (float)Time.ElapsedTime;

            var fireBufferEntity = GetSingletonEntity<FireBuffer>();
            var gridBufferLookup = GetBufferFromEntity<FireBufferElement>();
            var gridBuffer = gridBufferLookup[fireBufferEntity];

            var gridArray = gridBuffer.AsNativeArray();
            var gridMetaData = EntityManager.GetComponentData<FireBufferMetaData>(fireBufferEntity);

            // Create cached native array of temperature components to read neighbor temperature data in jobs
            NativeArray<TemperatureComponent> temperatureComponents = new NativeArray<TemperatureComponent>(gridArray.Length, Allocator.TempJob);

            var temperaturesFromEntity = GetComponentDataFromEntity<TemperatureComponent>(true);
            Dependency = new CopyTemperaturesFromFireGrid()
            {
                temperatures = temperaturesFromEntity,
                gridArray = gridArray,
                temperatureComponents = temperatureComponents
            }.Schedule(Dependency);

            Dependency.Complete();

            float frameLerp = deltaTime * 8f;
            // Update fires in scene
            Entities
                .WithDeallocateOnJobCompletion(temperatureComponents)
                .ForEach((Entity fireEntity, ref Translation position, ref TemperatureComponent temperature, ref FireColor color,
                    in BoundsComponent bounds, in StartHeight startHeight, in FireColorPalette pallete) =>
                {
                    var temp = math.clamp(temperature.Value, 0, 1);

                    // If temp is 0, velocity is put out
                    bool fireOut = UnityMathUtils.Approximately(temp, 0f, epsilon);
                    if (fireOut)
                    {
                        temperature.Velocity = 0;

                        // Find neighboring temperatures
                        var neighbors = GetNeighboringIndicies(temperature.GridIndex, gridMetaData.CountX, gridMetaData.CountZ);
                        var topTemp = (neighbors.Top == -1) ? 0 : temperatureComponents[neighbors.Top].Value;
                        var bottomTemp = (neighbors.Bottom == -1) ? 0 : temperatureComponents[neighbors.Bottom].Value;
                        var leftTemp = (neighbors.Left == -1) ? 0 : temperatureComponents[neighbors.Left].Value;
                        var rightTemp = (neighbors.Right == -1) ? 0 : temperatureComponents[neighbors.Right].Value;

                        // Find max temp of neighbors
                        var maxTemp = math.max(topTemp, bottomTemp);
                        maxTemp = math.max(maxTemp, leftTemp);
                        maxTemp = math.max(maxTemp, rightTemp);

                        float varianceTemp = 0.95f - temperature.IgnitionVariance;
                        if (maxTemp >= varianceTemp)
                        {
                            temperature.Velocity = temperature.StartVelocity * (1 + temperature.IgnitionVariance);
                        }
                    }

                    // Update temp with velocity
                    var deltaVel = temperature.Velocity * deltaTime;
                    temperature.Value = math.clamp(temp + deltaVel, 0, 1);

                    // Compute variance for fire height fluctation
                    float fireVariance = math.sin(5 * temperature.Value * elapsedTime + 100 * (1 + temperature.IgnitionVariance)) * startHeight.Variance * temperature.Value;

                    // Compute new height
                    float newHeight = bounds.SizeY / 2f + fireVariance;
                    float heightFrameTarget = startHeight.Value + newHeight * temperature.Value;
                    position.Value.y = math.lerp(position.Value.y, heightFrameTarget, frameLerp);

                    // Update fire color
                    float4 newColor = fireOut ? pallete.UnlitColor : math.lerp(pallete.LitLowColor, pallete.LitHighColor, temperature.Value);
                    color.Value = math.lerp(color.Value, newColor, frameLerp);

                }).ScheduleParallel();
        }

        public struct Neighbors
        {
            public int Top;
            public int Bottom;
            public int Left;
            public int Right;
        }

        /// <summary>
        /// Finds indicies for neighbors. If they do not exist, will return -1
        /// </summary>
        /// <param name="index"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        public static Neighbors GetNeighboringIndicies(int index, int width, int height)
        {
            int indexCol = index % width;
            int indexRow = index / height;

            int topRow = math.clamp(indexRow - 1, 0, height - 1);
            int bottomRow = math.clamp(indexRow + 1, 0, height - 1);

            int leftCol = math.clamp(indexCol - 1, 0, width - 1);
            int rightCol = math.clamp(indexCol + 1, 0, width - 1);

            int topIndex = width * topRow + indexCol;
            int bottomIndex = width * bottomRow + indexCol;
            int leftIndex = width * indexRow + leftCol;
            int rightIndex = width * indexRow + rightCol;

            return new Neighbors
            {
                // If neighbor index matches current index, then that means the neighbor doesn't exist
                Top = topIndex == index ? -1 : topIndex,
                Bottom = bottomIndex == index ? -1 : bottomIndex,
                Left = leftIndex == index ? -1 : leftIndex,
                Right = rightIndex == index ? -1 : rightIndex
            };
        }
    }
}
