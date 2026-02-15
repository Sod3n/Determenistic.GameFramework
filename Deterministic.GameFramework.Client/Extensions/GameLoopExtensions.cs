using System;
using Deterministic.GameFramework.Core;
using R3;

namespace Deterministic.GameFramework.Client.Extensions
{
    public static class GameLoopExtensions
    {
        /// <summary>
        /// Given an observable tick deadline, emits the remaining time in milliseconds
        /// every tick until the deadline is reached. Emits 0 when deadline has passed.
        /// When deadline is 0 (no deadline), emits -1.
        /// </summary>
        public static Observable<int> TimeTo(this GameLoop gameLoop, Observable<long> deadlineTick)
        {
            return deadlineTick.Select(deadline =>
            {
                if (deadline <= 0)
                    return Observable.Return(-1);

                return Observable.EveryUpdate()
                    .Select(_ =>
                    {
                        var remaining = deadline - gameLoop.CurrentTick;
                        return Math.Max(0, (int)(remaining * gameLoop.FixedDeltaTime * 1000));
                    })
                    .DistinctUntilChanged();
            }).Switch();
        }
    }
}
