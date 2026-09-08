_dotisan_complete() {
  local commands="new make add remove make:resource make:endpoint make:crud migrate dev build run generate jobs schedule doctor mail add:integration remove:integration help"
  COMPREPLY=( $(compgen -W "$commands" -- "${COMP_WORDS[1]}") )
}
complete -F _dotisan_complete dotisan
