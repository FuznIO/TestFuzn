using Fuzn.TestFuzn.Internals.State;

namespace Fuzn.TestFuzn.Internals.InputData;

internal class InputDataFeeder
{
    private object _lock = new();
    private Dictionary<string, InputEnumeratorInfo> _feeder = new();
    private TestExecutionState _testExecutionState = null!;

    public void Init(TestExecutionState testExecutionState)
    {
        _testExecutionState = testExecutionState;

        foreach (var scenario in _testExecutionState.Scenarios)
        {
            var enumeratorInfo = new InputEnumeratorInfo
            {
                InputData = scenario.InputDataInfo.InputDataList,
                EndOfInputBehavior = scenario.InputDataInfo.InputDataBehavior,
                Index = -1
            };
            _feeder[scenario.Name] = enumeratorInfo;
        }
    }

    public object GetNextInput(string scenario)
    {
        lock (_lock)
        {
            var info = _feeder[scenario];
            bool isLastItem = info.Index == info.InputData.Count - 1;

            switch (info.EndOfInputBehavior)
            {
                case InputDataBehavior.Loop:
                    {
                        if (isLastItem)
                            info.Index = 0;
                        else
                            info.Index++;
                        break;
                    }
                case InputDataBehavior.LoopThenRepeatLast:
                    {
                        if (!isLastItem)
                            info.Index++;
                        break;
                    }
                case InputDataBehavior.LoopThenRandom:
                    {
                        if (isLastItem)
                            info.HasReachedEndOfInputData = true;

                        if (info.HasReachedEndOfInputData)
                            info.Index = Random.Shared.Next(0, info.InputData.Count);
                        else
                            info.Index++;
                        break;
                    }
                case InputDataBehavior.Random:
                    {
                        info.Index = Random.Shared.Next(0, info.InputData.Count);
                        break;
                    }
            }

            return info.InputData[info.Index];           
        }
    }

    internal class InputEnumeratorInfo
    {
        public InputDataBehavior EndOfInputBehavior;
        public List<object> InputData;
        public int Index;
        public bool HasReachedEndOfInputData = false;
    }
}

