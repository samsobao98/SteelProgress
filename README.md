# SteelProgress

**SteelProgress** es una aplicación de escritorio diseñada para registrar y analizar entrenamientos de fuerza.
Será realizada por Samuel Rodríguez González, y utilizará ChatGPT como herramienta para realizar dicho proyecto.
Permitirá al usuario:

- Crear rutinas de entrenamiento
- Registrar sesiones con series, repeticiones y peso
- Consultar el historial de entrenamientos
- Visualizar el progreso a lo largo del tiempo

En este README se irán documentando los **avances diarios del proyecto**.

---

# Progreso del proyecto

## 11/03/2026 — Creación del proyecto

Se crea la solución en Visual Studio con arquitectura por capas:

- `SteelProgress.App` (WPF)
- `SteelProgress.Domain` (modelo)
- `SteelProgress.Data` (acceso a datos)
- `SteelProgress.Tests` (pruebas)

Se establece la base estructural del proyecto.

---

## 12/03/2026 — Modelo de datos y base de datos

Se define la entidad `Exercise` y se configura **Entity Framework Core con SQLite**.

Se crea `AppDbContext`, se genera la migración inicial y la base de datos con la tabla `Exercises`.

---

## 16/03/2026 — CRUD de ejercicios

Se implementa la gestión completa de ejercicios:

- Crear, leer, actualizar y eliminar
- Validaciones básicas

Se conecta WPF con la base de datos mediante EF Core.

---

## 18/03/2026 — Mejora de arquitectura

Se introduce el patrón **Repository** (`ExerciseRepository`) y se inicia MVVM:

- `BaseViewModel`
- `ExerciseViewModel`

Se separa la lógica de la interfaz.

---

## 23/03/2026 — MVVM completo

Se implementa MVVM completo:

- Binding en XAML
- Commands (`ICommand`)
- Eliminación de lógica en code-behind

Se inicia el módulo de rutinas con nuevas entidades:

- `Routine`
- `RoutineDay`
- `RoutineDayExercise`

---

## 01/04/2026 — Gestión básica de rutinas

Se implementa la funcionalidad inicial de rutinas:

- `RoutineRepository`
- `RoutineViewModel`
- Ventana `RoutineWindow`

Permite crear, listar y eliminar rutinas.

---

## 02/04/2026 — Días de rutina

Se comprime la información proporcionada de cada día trabajado en este readme con la
ayuda de la IA.

Se amplía el sistema para gestionar días dentro de cada rutina:

- Añadir y eliminar `RoutineDay`
- Carga dinámica de días al seleccionar una rutina
- Segunda tabla en la interfaz

Se corrigen problemas de UI y actualización de datos.

Resultado:

- Estructura completa: Rutina → Días

  ## 04/04/2026 — Sistema de entrenamiento

Con la ayuda de la IA, Se completó la estructura de entrenamiento:

- Añadir y gestionar ejercicios dentro de cada día (`RoutineDayExercise`)
- Visualización de ejercicios por día

Se implementó el sistema de sesiones:

- `WorkoutSession`
- `WorkoutExercise`
- `WorkoutSet`

Se desarrolló `WorkoutRepository` para:

- Crear sesiones a partir de un día de rutina
- Copiar automáticamente los ejercicios
- Registrar sets (peso y repeticiones)

Resultado:

- Flujo completo implementado:
  Rutina → Día → Ejercicios → Sesión → Sets
- Backend principal del sistema finalizado

  ## 19/04/2026 — Historial de entrenamientos

Se implementó la visualización del historial de sesiones de entrenamiento.

Se desarrolló `HistoryViewModel` para:

- Obtener todas las sesiones registradas
- Gestionar la selección de una sesión
- Cargar el detalle de ejercicios asociados

Se creó la ventana `HistoryWindow`, que permite:

- Visualizar la lista de sesiones (fecha y día de rutina)
- Seleccionar una sesión
- Mostrar los ejercicios realizados y su número de series

Se integró el acceso desde la ventana principal mediante un botón de navegación.

Resultado:

- Consulta de sesiones implementada en UI
- Visualización básica del historial de entrenamientos
- Integración completa con el backend existente

## 21/04/2026 — Gráfica de progreso y rediseño visual

Se implementó la visualización del progreso de entrenamiento mediante una gráfica.

Con la ayuda de la IA, se integró la librería **LiveCharts2** en el proyecto WPF para permitir la representación visual de datos.

Se desarrolló `ProgressViewModel`, encargado de:

- Cargar los ejercicios disponibles
- Gestionar la selección de ejercicio
- Calcular el peso máximo por sesión
- Generar los datos necesarios para la gráfica

Se creó la ventana `ProgressWindow`, que permite:

- Seleccionar un ejercicio
- Visualizar su evolución a lo largo del tiempo mediante una gráfica

---

Con la ayuda de la IA, se realizó un primer rediseño visual completo de la aplicación aplicando un tema oscuro estilo “gym”.

Se definieron recursos globales en `App.xaml`:

- Paleta de colores (fondo, texto, acentos)
- Estilos globales para Button, TextBlock, TextBox, ComboBox y DataGrid

---

Se rediseñaron todas las ventanas principales:

- `MainWindow`
- `WorkoutWindow`
- `HistoryWindow`
- `RoutineWindow`
- `ProgressWindow`

Aplicando:

- Estructura basada en bloques tipo “card” (`Border`)
- Cabeceras claras con títulos y subtítulos
- Mejor distribución del espacio
- Formularios más compactos
- Tablas integradas en el diseño

---

Se mejoró la experiencia visual:

- Implementación de fondo oscuro en toda la aplicación
- Mejora de contraste y legibilidad
- Estilizado de ComboBox y elementos desplegables
- Consistencia visual global

---

Resultado:

- Visualización del progreso mediante gráfica funcional
- Interfaz moderna y coherente
- Integración completa entre funcionalidad y diseño
- Base sólida para pulido final y presentación

## 22/04/2026 — Navegación unificada y dashboard inicial

Se barajó con la IA posibles mejoras de diseño que ayuden a mejorar la experiencia del usuario. Primero, se rediseñó la arquitectura de la interfaz para eliminar el uso de múltiples ventanas independientes.

---

### Unificación de navegación

Se refactorizó la aplicación para trabajar con una única ventana principal (`MainWindow`), que ahora actúa como contenedor de vistas dinámicas.

Se implementó el uso de `UserControl` para cada módulo:

- `RoutineView`
- `WorkoutView`
- `HistoryView`
- `ProgressView`

Estas vistas se cargan dinámicamente en un `ContentControl`, evitando la apertura de nuevas ventanas.

---

### Eliminación de ventanas independientes

Se dejó de utilizar el sistema anterior basado en múltiples `Window`, sustituyéndolo por navegación interna dentro de la aplicación.

Esto mejora:

- Fluidez de navegación
- Experiencia de usuario
- Sensación de aplicación profesional

---

### Implementación de menú lateral

Con ayuda de la IA, se diseñó e implementó un menú lateral (sidebar) con navegación entre secciones:

- Inicio
- Rutinas
- Entrenamiento
- Historial
- Progreso

El menú incluye funcionalidad colapsable:

- Modo expandido (icono + texto)
- Modo compacto (solo iconos)

---

### Creación de pantalla inicial (HomeView)

Se añadió una pantalla principal tipo dashboard (`HomeView`) que se muestra al iniciar la aplicación.

Incluye:

- Mensaje de bienvenida
- Accesos rápidos a módulos
- Estadísticas generales:
  - Número de ejercicios
  - Número de rutinas
  - Número de sesiones
  - Último entrenamiento
- Actividad reciente (últimas sesiones)

Se implementó `HomeViewModel` para gestionar los datos mostrados.

---

### Resultado

- Navegación unificada en una sola ventana
- Mejora significativa de la experiencia de usuario
- Interfaz más profesional y moderna
- Base sólida para mejoras de UX y visualización avanzada

## 23/04/2026 — Mejora de usabilidad y organización visual

Durante esta sesión se realizaron mejoras centradas en la experiencia de usuario y en la claridad visual de la aplicación.

---

### Reorganización del módulo de rutinas

Se rediseñó `RoutineView` para evitar la sobrecarga de información en una única pantalla.

Se sustituyó la estructura anterior por un sistema basado en `TabControl`, dividiendo la funcionalidad en tres pestañas:

- Rutinas
- Días
- Ejercicios

Esto permite:

- Separar responsabilidades dentro de la interfaz
- Mejorar la claridad del flujo de uso
- Reducir la sensación de pantalla saturada

---

### Mejora visual del sistema de pestañas

Se personalizó el estilo de `TabControl` y `TabItem`:

- Adaptación al tema oscuro
- Uso de colores de acento en pestañas activas
- Mejora del feedback visual (hover y selección)
- Integración estética con el resto de la aplicación

Además, se implementaron encabezados personalizados con iconos para cada pestaña, mejorando la identificación visual de cada sección.

---

### Mejora de la visualización de datos

Se ajustó el estilo global de los `DataGrid` utilizados en la aplicación:

- Mejora de tipografía y espaciado
- Aumento de la altura de filas
- Alternancia de colores en filas
- Resaltado de fila seleccionada
- Mejora del comportamiento en hover

Esto proporciona una visualización más clara y profesional de:

- Rutinas
- Días
- Ejercicios
- Historial

---

### Revisión y ajuste de estilos globales

Se revisó la configuración de `App.xaml`, asegurando:

- Consistencia visual en todos los controles
- Compatibilidad con el arranque de la aplicación (`StartupUri`)
- Integración de estilos sin afectar al comportamiento existente

---

### Resultado

- Interfaz más clara y estructurada
- Mejora significativa en la usabilidad del módulo de rutinas
- Consistencia visual en todos los componentes
- Sensación más cercana a una aplicación profesional

