Register-ArgumentCompleter -Native -CommandName dotisan -ScriptBlock {
    param($wordToComplete, $commandAst, $cursorPosition)
    $commands = @('new','make','add','remove','make:resource','make:endpoint','make:crud','migrate','dev','build','run','generate','jobs','schedule','doctor','mail','add:integration','remove:integration','help')
    $commands | Where-Object { $_ -like "$wordToComplete*" } | ForEach-Object {
        [System.Management.Automation.CompletionResult]::new($_, $_, 'Command', $_)
    }
}
