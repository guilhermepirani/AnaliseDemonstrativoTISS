# Copilot Instructions

## Project Guidelines
- O usuário prefere refatorações guiadas por princípios SOLID, KISS e DRY, com centralização de lógica comum sem prejudicar legibilidade.
- Quando classes derivadas de RegistroAnalise tiverem propriedades adicionais, as colunas dessas propriedades devem ser posicionadas depois das colunas da classe base na tabela.

## Testing Guidelines
- Em testes, usar xUnit v3 + FluentAssertions, priorizar código simples e reutilizável, usar AutoFixture quando apropriado e evitar Moq quando possível, utilizando classes/métodos reais.