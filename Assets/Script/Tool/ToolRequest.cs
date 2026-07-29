using System;

[Serializable]
public class ToolRequest: Request
{
    private Tool tool;
    //public GameObject targetClient;
    //public OperationChair targetChair;
    public Tool Tool => tool;
    public ToolRequest(Tool tool, float requestTime): base(tool.Type, tool.Name, requestTime)
    {
        this.tool = tool;
    }
}