using Fuzn.TestFuzn.Internals.State;

namespace Fuzn.TestFuzn.Internals.InputData;

internal class InputDataFeeder
{
    private Dictionary<string, object> _locks = new();
    private Dictionary<string, InputEnumeratorInfo> _feeder = new();
    private TestExecutionState _testExecutionState = null!;

    public void Init(TestExecutionState testExecutionState)
    {
        _testExecutionState = testExecutionState;
        var cluster = testExecutionState.TestSession.Cluster;

        foreach (var scenario in _testExecutionState.Scenarios)
        {
            var inputData = scenario.InputDataInfo.InputDataList;
            var enumeratorInfo = new InputEnumeratorInfo
            {
                InputData = inputData,
                Slice = Slice(inputData, cluster.WorkerIndex, cluster.WorkerCount),
                WorkerIndex = cluster.WorkerIndex,
                WorkerCount = cluster.WorkerCount,
                EndOfInputBehavior = scenario.InputDataInfo.InputDataBehavior,
                Index = -1
            };
            _locks[scenario.Name] = new object();
            _feeder[scenario.Name] = enumeratorInfo;
        }
    }

    // Worker i owns the items at i, i+N, i+2N, ... so no item is used on two workers at the same time
    // while looping. The leftover rule matches the load split, so OneTimeLoad(items.Count) uses each item once.
    internal static List<object> Slice(List<object>? inputData, int workerIndex, int workerCount)
    {
        if (inputData == null)
            return new List<object>();
        if (workerCount <= 1)
            return inputData;

        var slice = new List<object>();
        for (var index = workerIndex; index < inputData.Count; index += workerCount)
            slice.Add(inputData[index]);

        return slice;
    }

    public object GetNextInput(string scenario)
    {
        var info = _feeder[scenario];
        lock (_locks[scenario])
        {
            switch (info.EndOfInputBehavior)
            {
                case InputDataBehavior.Loop:
                    {
                        if (info.Slice.Count == 0)
                        {
                            var inputDataCount = 0;
                            if (info.InputData != null)
                                inputDataCount = info.InputData.Count;
                            throw new InvalidOperationException(
                                $"Load worker {info.WorkerIndex} of {info.WorkerCount} has no input data to loop over in scenario '{scenario}': " +
                                $"the list has {inputDataCount} items for {info.WorkerCount} workers. Use more input data or fewer workers.");
                        }

                        info.Index = info.Index == info.Slice.Count - 1 ? 0 : info.Index + 1;
                        return info.Slice[info.Index];
                    }
                case InputDataBehavior.LoopThenRepeatLast:
                    {
                        if (!info.HasReachedEndOfInputData && info.Index < info.Slice.Count - 1)
                        {
                            info.Index++;
                            return info.Slice[info.Index];
                        }

                        info.HasReachedEndOfInputData = true;
                        return info.InputData[info.InputData.Count - 1];
                    }
                case InputDataBehavior.LoopThenRandom:
                    {
                        if (!info.HasReachedEndOfInputData && info.Index < info.Slice.Count - 1)
                        {
                            info.Index++;
                            return info.Slice[info.Index];
                        }

                        info.HasReachedEndOfInputData = true;
                        return info.InputData[Random.Shared.Next(0, info.InputData.Count)];
                    }
                case InputDataBehavior.Random:
                    {
                        return info.InputData[Random.Shared.Next(0, info.InputData.Count)];
                    }
                default:
                    throw new NotSupportedException($"Input data behavior {info.EndOfInputBehavior} is not supported.");
            }
        }
    }

    internal class InputEnumeratorInfo
    {
        public InputDataBehavior EndOfInputBehavior;
        public List<object> InputData;
        public List<object> Slice;
        public int WorkerIndex;
        public int WorkerCount;
        public int Index;
        public bool HasReachedEndOfInputData = false;
    }
}
