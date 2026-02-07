# GitHub Copilot Instructions

## 1. Language and Communication
- **English Mandatory:** All code (variables, functions, classes), comments, and documentation must be written in **English** only.
- **Tone:** Technical, concise, and professional.

## 2. Coding Standards
- **One Class Per File:** Each file must contain only one single class. This rule applies to all classes, including **Serializable** classes (DTOs, Models, etc.).
- **Explicit Member Access:** Always use the `this` keyword when accessing class members (fields, properties, and methods) to ensure clarity.
- **Clean Code:** Adhere to SOLID principles and keep functions small and focused.
- **Naming Conventions:** - Use `camelCase` for variables and functions.
    - Use `PascalCase` for classes and types.
    - Use `SCREAMING_SNAKE_CASE` for constants.

## 3. Inline Comments Only
- **No External Documentation:** Do not generate separate documentation files (e.g., .txt, .docs, or separate .md manuals).
- **Code Comments:** Use brief inline comments to explain the "why" behind complex logic directly within the source files.

## 4. No Debug Logs
- **No Console/Debug Logs:** Strictly do not include `console.log`, `print`, `var_dump`, or any other debug logging statements in the final code output.

## 5. Unity Specific Rules
- **No ContextMenu:** Do not use the `[ContextMenu]` attribute in Unity scripts. Use other methods for editor tools if necessary.