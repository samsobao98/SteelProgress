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

## 12/03/2026 — Modelo de datos y base de datos

Durante esta sesión se comenzó a preparar la estructura de datos del proyecto.

Se creó la carpeta `Entities` en `SteelProgress.Domain` y dentro de ella la entidad `Exercise`, que representará los ejercicios registrados en la aplicación. Esta entidad incluye los campos `Id`, `Name`, `MuscleGroup` y `Notes`.

En `SteelProgress.Data` se creó la carpeta `Context` y la clase `AppDbContext`, que será la encargada de conectar el modelo de datos con la base de datos mediante **Entity Framework Core**.

También se configuró **SQLite** como motor de base de datos y se creó la clase `AppDbContextFactory`, necesaria para poder generar migraciones.

Por último, se generó la migración inicial (`InitialCreate`) y se creó la base de datos `steelprogress.db`, incluyendo la tabla `Exercises`.

## 16/03/2026 — Implementación del CRUD de ejercicios

Durante esta sesión se conectó la aplicación WPF con la base de datos y se implementó la gestión completa de ejercicios.

### Conexión de la aplicación con la base de datos

Se configuró el `AppDbContext` en el archivo `App.xaml.cs`, permitiendo que la aplicación utilice **Entity Framework Core** con **SQLite** al iniciarse.

Se añadió la llamada a `Database.Migrate()` para asegurar que la base de datos
y sus tablas se crean automáticamente al arrancar la aplicacion.

---

### Pruebas iniciales de acceso a datos

Se realizaron pruebas desde `MainWindow` para verificar:

- Inserción de datos en la base de datos
- Lectura de registros
- Persistencia de la información entre ejecuciones

Esto permitió confirmar que la conexión con SQLite funciona correctamente.

---

### Implementación de la interfaz de gestión de ejercicios

Se diseñó una primera interfaz en `MainWindow` que permite:

- Introducir datos de un ejercicio (nombre, grupo muscular y notas)
- Visualizar los ejercicios almacenados en un `DataGrid`

---

### Funcionalidad CRUD de ejercicios

Se implementaron las operaciones básicas:

- **Create** → añadir nuevos ejercicios mediante formulario
- **Read** → mostrar los ejercicios en una tabla
- **Update** → modificar ejercicios seleccionados
- **Delete** → eliminar ejercicios con confirmación

Además, se añadieron validaciones:

- Campos obligatorios (nombre y grupo muscular)
- Control de duplicados por nombre

---

### Resultado

Al finalizar la sesión se dispone de un sistema completo de gestión de ejercicios con:

- Persistencia en base de datos SQLite
- Interfaz funcional en WPF
- Operaciones CRUD completas
- Validación de datos

Este módulo constituye la base sobre la que se desarrollarán el resto de funcionalidades del proyecto.

## 18/03/2026 — Mejora de arquitectura y primer paso hacia MVVM

Durante esta sesión se continuó el desarrollo del módulo de ejercicios, mejorando la arquitectura del proyecto y separando responsabilidades entre capas.

### Uso de Repository

Se integró el `ExerciseRepository` en la aplicación, eliminando el acceso directo al `DbContext` desde la interfaz (`MainWindow`).

Se añadieron métodos en el repositorio para gestionar:

- Obtención de ejercicios
- Inserción de nuevos registros
- Actualización de ejercicios
- Eliminación de ejercicios
- Validación de duplicados

Esto permite desacoplar la lógica de acceso a datos de la interfaz de usuario, mejorando la mantenibilidad del código.

---

### Refactorización del CRUD

Se modificaron los métodos de la interfaz para utilizar el repositorio en lugar de acceder directamente a la base de datos.

Con esto se consigue una arquitectura más limpia:

UI → Repository → DbContext → SQLite

---

### Implementación del patrón MVVM (inicio)

Se dio el primer paso hacia el patrón **MVVM (Model-View-ViewModel)**.

Se creó:

- `BaseViewModel`, implementando `INotifyPropertyChanged`
- `ExerciseViewModel`, encargado de gestionar el estado y la lógica de los ejercicios

El ViewModel incluye:

- Colección observable de ejercicios (`ObservableCollection`)
- Propiedades para el formulario (Name, MuscleGroup, Notes)
- Gestión del elemento seleccionado
- Métodos para cargar datos y limpiar el formulario

---

### Integración ViewModel - Vista

Se conectó parcialmente el `ExerciseViewModel` con la vista (`MainWindow`), trasladando parte de la lógica desde la interfaz hacia el ViewModel.

---

### Resultado

Al finalizar la sesión, el proyecto cuenta con:

- Arquitectura en capas consolidada
- Uso del patrón Repository
- Separación parcial de lógica mediante MVVM
- Código más modular y mantenible

Este paso supone una mejora importante en la calidad del proyecto y facilita su escalabilidad para futuras funcionalidades.

## 23/03/2026 — MVVM completo e inicio del módulo de rutinas

Durante esta sesión se mejoró la arquitectura de la aplicación implementando MVVM de forma más completa y se inició el desarrollo del módulo de rutinas.

### Implementación de binding en XAML

Se sustituyó el acceso directo a los controles desde el code-behind por **data binding**, conectando la interfaz con el `ExerciseViewModel`.

Se enlazaron:

- TextBox → propiedades del ViewModel (`Name`, `MuscleGroup`, `Notes`)
- DataGrid → colección de ejercicios (`Exercises`)
- SelectedItem → ejercicio seleccionado (`SelectedExercise`)

Esto permite que la interfaz se actualice automáticamente sin manipular los controles desde código.

---

### Uso de Commands (ICommand)

Se eliminaron los eventos `Click` de los botones y se implementaron **Commands** mediante la clase `RelayCommand`.

Se añadieron:

- `AddExerciseCommand`
- `UpdateExerciseCommand`
- `DeleteExerciseCommand`

Toda la lógica de las acciones se pasó al `ExerciseViewModel`, eliminando dependencia del code-behind.

---

### Eliminación de lógica en la vista

Se eliminaron elementos del `MainWindow.xaml.cs`:

- Métodos de eventos de botones
- Método `ClearForm`
- Evento `SelectionChanged` del DataGrid

La selección de elementos ahora se gestiona mediante binding, utilizando la propiedad `SelectedExercise` del ViewModel.

---

### Mejora del patrón MVVM

Se consolidó el uso del patrón MVVM con:

- `BaseViewModel` (`INotifyPropertyChanged`)
- `ExerciseViewModel` como gestor de estado y lógica
- Separación clara entre interfaz y lógica de negocio

El flujo actual queda:

View → ViewModel → Repository → DbContext → SQLite

---

### Inicio del módulo de rutinas

Se comenzó el desarrollo de la funcionalidad de rutinas creando las siguientes entidades:

- `Routine`
- `RoutineDay`
- `RoutineDayExercise`

Estas permiten estructurar rutinas con múltiples días y ejercicios asociados.

Se añadieron los correspondientes `DbSet` en el `AppDbContext` y se generó una nueva migración para crear las tablas en la base de datos.

---

### Resultado

Al finalizar la sesión, el proyecto cuenta con:

- Implementación de MVVM con binding y commands
- Eliminación casi total de lógica en la vista
- Arquitectura más limpia y mantenible
- Base estructural para el módulo de rutinas

Este avance supone un salto importante en la calidad del código y en la escalabilidad de la aplicación.
