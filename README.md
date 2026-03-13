# SteelProgress

**SteelProgress** es una aplicación de escritorio diseñada para registrar y analizar entrenamientos de fuerza.  
Permitirá al usuario:

- Crear rutinas de entrenamiento
- Registrar sesiones con series, repeticiones y peso
- Consultar el historial de entrenamientos
- Visualizar el progreso a lo largo del tiempo

En este README se irán documentando los **avances diarios del proyecto**.

---

# Progreso del proyecto

## 11/03/2026 — Creación del proyecto

Se crea una **solución en blanco** en Visual Studio para organizar el proyecto en distintas capas.

Dentro de la solución se crean los siguientes proyectos:

### `SteelProgress.App`
Proyecto **WPF** encargado de ejecutar la aplicación y gestionar la interfaz gráfica.

Depende de:
- `SteelProgress.Domain`
- `SteelProgress.Data`

---

### `SteelProgress.Domain`
Contiene:

- Entidades del dominio
- Enums
- Lógica del modelo de datos

Aquí se definen las estructuras principales del sistema.

---

### `SteelProgress.Data`
Encargado del **acceso a datos**.

Este proyecto gestionará:

- Entity Framework Core
- La conexión con la base de datos **SQLite**
- Configuración del `DbContext`

Depende de:
- `SteelProgress.Domain`

---

### `SteelProgress.Tests`
Proyecto destinado a **pruebas unitarias** del sistema.

Permitirá comprobar el correcto funcionamiento de la lógica del dominio y de los servicios de datos.
