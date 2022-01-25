using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace SmartComponentDeduction
{
    public class TransactionFailuresProcessor : IFailuresPreprocessor
    {
        public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
        {
            // 获取所有的失败信息
            IList<FailureMessageAccessor> failureMessageAccessors = failuresAccessor.GetFailureMessages();
            if (failureMessageAccessors.Count == 0)
                return FailureProcessingResult.Continue;
            foreach (FailureMessageAccessor failureMessAcce in failureMessageAccessors)
            {
                // 如果是错误，则尝试解决
                if (failureMessAcce.GetSeverity() == FailureSeverity.Error)
                {
                    // 模拟手动单击"删除连接"按钮
                    if (failureMessAcce.HasResolutions())
                        failuresAccessor.ResolveFailure(failureMessAcce);
                }
                // 如果是警告，则禁止弹框
                if (failureMessAcce.GetSeverity() == FailureSeverity.Warning)
                {
                    failuresAccessor.DeleteWarning(failureMessAcce);
                }
            }
            return FailureProcessingResult.ProceedWithCommit;
        }
    }
}