namespace Fuzn.TestFuzn.Internals.InputData;

internal class InputDataInfo
{
    public bool HasInputData { get; set; } = false;
    public InputDataSourceType SourceType { get; set; }
    public List<object> InputDataList { get; set; }
    public InputDataBehavior InputDataBehavior { get; set; } = InputDataBehavior.Loop;
    public Func<Context, Task<List<object>>> InputDataAction { get; set; }

    public void AddParams(params object[] inputData)
    {
        HasInputData = true;
        SourceType = InputDataSourceType.Params;
        if (InputDataList == null)
            InputDataList = new();

        InputDataList.AddRange(inputData);
    }

    public void AddAction(Func<Context, Task<List<object>>> action)
    {
        HasInputData = true;
        SourceType = InputDataSourceType.Action;
        InputDataAction = action;
    }
}