using Fuzn.TestFuzn.Contracts.Results.Standard;

namespace Fuzn.TestFuzn;

internal sealed class Step
{
    public string Name { get; set; }
    public string Id { get; set; }
    public string ParentName { get; set; }
    public Func<IterationContext, Task> Action { get; set; }
    public Type ContextType { get; set; }
    public List<Step> Steps { get; set; }
    public List<Comment> Comments { get; set; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
            throw new InvalidOperationException();

        if (Action == null)
            throw new InvalidOperationException();

        if (ContextType == null)
            throw new InvalidOperationException();

        if (Steps != null)
        {
            for (int i = 0; i < Steps.Count; i++)
            {
                Steps[i].Validate();
            }
        }
    }
}