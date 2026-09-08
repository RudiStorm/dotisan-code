# Add this file to fpath and autoload it, or source it from .zshrc.
_dotisan() {
  local -a commands
  commands=(new make add remove make:resource make:endpoint make:crud migrate dev build run generate jobs schedule doctor mail add:integration remove:integration help)
  _describe 'dotisan command' commands
}
compdef _dotisan dotisan
