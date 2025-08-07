using Unity.Collections;

namespace Unity.Physics.Stateful
{
    // StatefulSimulationEventBuffers는 이벤트의 상태를 추적하는 두 버퍼를 관리하는 구조체입니다.
    // 이 구조체는 지난 프레임과 현재 프레임의 이벤트를 저장하며, 충돌 이벤트에 대한 상태 변경을 추적합니다.
    public struct StatefulSimulationEventBuffers<T> where T : unmanaged, IStatefulSimulationEvent<T>
    {
        // 이전 프레임의 이벤트를 저장하는 리스트
        public NativeList<T> Previous;
        // 현재 프레임의 이벤트를 저장하는 리스트
        public NativeList<T> Current;

        // 이벤트 버퍼 할당
        public void AllocateBuffers()
        {
            // 이전 및 현재 프레임 이벤트를 저장할 버퍼를 할당
            Previous = new NativeList<T>(Allocator.Persistent);
            Current = new NativeList<T>(Allocator.Persistent);
        }

        // 이벤트 버퍼를 해제
        public void Dispose()
        {
            // 버퍼가 생성되었으면 해제
            if (Previous.IsCreated) Previous.Dispose();
            if (Current.IsCreated) Current.Dispose();
        }

        // 이전 프레임과 현재 프레임의 버퍼를 교환
        public void SwapBuffers()
        {
            // 이전과 현재 이벤트 버퍼를 교환
            var tmp = Previous;
            Previous = Current;
            Current = tmp;
            // 현재 버퍼를 비움 (새로운 이벤트로 덮어쓰기 위해)
            Current.Clear();
        }

        /// <summary>
        /// 이전 프레임과 현재 프레임의 이벤트를 결합하여 하나의 정렬된 리스트로 반환합니다.
        /// 반드시 SortBuffers를 먼저 호출하여 정렬된 상태로 만들어야 합니다.
        /// </summary>
        /// <param name="statefulEvents">결합된 이벤트 리스트</param>
        /// <param name="sortCurrent">현재 이벤트 리스트를 정렬해야 하는지 여부</param>
        public void GetStatefulEvents(NativeList<T> statefulEvents, bool sortCurrent = true) =>
            GetStatefulEvents(Previous, Current, statefulEvents, sortCurrent);

        /// <summary>
        /// 두 개의 정렬된 이벤트 버퍼를 결합하여 하나의 리스트로 반환합니다.
        /// 각 이벤트에는 적절한 StatefulEventState가 설정됩니다.
        /// </summary>
        /// <param name="previousEvents">이전 프레임의 이벤트 버퍼</param>
        /// <param name="currentEvents">현재 프레임의 이벤트 버퍼</param>
        /// <param name="statefulEvents">결합된 이벤트 리스트</param>
        /// <param name="sortCurrent">현재 이벤트 리스트를 정렬해야 하는지 여부</param>
        public static void GetStatefulEvents(NativeList<T> previousEvents, NativeList<T> currentEvents, NativeList<T> statefulEvents, bool sortCurrent = true)
        {
            // 현재 이벤트 리스트가 정렬되지 않았다면 정렬
            if (sortCurrent) currentEvents.Sort();

            // 새로운 리스트를 비움
            statefulEvents.Clear();

            int c = 0; // 현재 이벤트 리스트의 인덱스
            int p = 0; // 이전 이벤트 리스트의 인덱스

            // 현재 이벤트와 이전 이벤트를 비교하며 상태 변경
            while (c < currentEvents.Length && p < previousEvents.Length)
            {
                // 두 이벤트를 비교
                int r = previousEvents[p].CompareTo(currentEvents[c]);
                if (r == 0)
                {
                    // 현재 이벤트가 이전 이벤트와 동일하다면 'Stay' 상태로 추가
                    var currentEvent = currentEvents[c];
                    currentEvent.State = StatefulEventState.Stay;
                    statefulEvents.Add(currentEvent);
                    c++;
                    p++;
                }
                else if (r < 0)
                {
                    // 이전 이벤트가 현재 이벤트보다 먼저 왔다면 'Exit' 상태로 추가
                    var previousEvent = previousEvents[p];
                    previousEvent.State = StatefulEventState.Exit;
                    statefulEvents.Add(previousEvent);
                    p++;
                }
                else //(r > 0)
                {
                    // 현재 이벤트가 이전 이벤트보다 먼저 왔다면 'Enter' 상태로 추가
                    var currentEvent = currentEvents[c];
                    currentEvent.State = StatefulEventState.Enter;
                    statefulEvents.Add(currentEvent);
                    c++;
                }
            }

            // 현재 이벤트가 다 처리되었고, 이전 이벤트가 남았다면 'Exit' 상태로 추가
            if (c == currentEvents.Length)
            {
                while (p < previousEvents.Length)
                {
                    var previousEvent = previousEvents[p];
                    previousEvent.State = StatefulEventState.Exit;
                    statefulEvents.Add(previousEvent);
                    p++;
                }
            }
            // 이전 이벤트가 다 처리되었고, 현재 이벤트가 남았다면 'Enter' 상태로 추가
            else if (p == previousEvents.Length)
            {
                while (c < currentEvents.Length)
                {
                    var currentEvent = currentEvents[c];
                    currentEvent.State = StatefulEventState.Enter;
                    statefulEvents.Add(currentEvent);
                    c++;
                }
            }
        }
    }
}