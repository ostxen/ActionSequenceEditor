using System;
using Unity.Pipeline.Commands;

namespace NBC.ActionEditor.Verification
{
    // 通过当前IDE重新生成工程文件，不改源码或设置。
    public static class ProjectSyncCommands
    {
        [CliGroupCommand("actioneditor.verification", "Regenerate current Unity IDE solution only; no source or settings changes.")]
        public static object SyncProjects([CliArg("confirm", "Regenerate generated project files.")] bool confirm = false,
            [CliArg("dry-run", "Preview only.")] bool dryRun = false)
        {
            VerificationGuard.Ready();
            if (dryRun) return new { operation = "Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll", scope = "generated solution and project files only" };
            if (!confirm) throw new InvalidOperationException("Preview and explicit approval required.");
            Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll(); return new { status = "passed" };
        }
    }
}