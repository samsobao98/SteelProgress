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

## 26/04/2026 — Pulido visual y mejoras de interfaz

Durante esta sesión se continuó con la mejora estética de la aplicación, centrando el trabajo en la coherencia visual y la calidad de la experiencia de usuario.

---

### Eliminación de elementos innecesarios

Se eliminaron los identificadores internos (`Id`) de las tablas mostradas en la interfaz:

- Rutinas
- Días
- Ejercicios
- Historial

---

### Mejora del espaciado en tablas

Se ajustó el espaciado interno de los `DataGrid`:

- Añadido padding en celdas y cabeceras
- Corrección del alineado del contenido en la primera columna
- Mejora general del “aire” entre elementos
  
---

### Personalización avanzada de DataGrid

Se reforzó el estilo global de las tablas:

- Mejora en tipografía
- Ajuste de altura de filas
- Colores alternos en filas
- Resaltado de fila seleccionada
- Feedback visual en hover

---

### Introducción a animaciones en interfaz

Se comenzó la mejora de interacciones mediante animaciones:

- Implementación de transición suave en hover de botones
- Uso de `Storyboard` para animar cambios de color
- Aplicación de animación sobre elementos `Border` en lugar de controles directamente

Esto permite:

- Evitar cambios bruscos de color
- Mejorar la sensación de fluidez
- Aumentar la calidad percibida de la interfaz

---

### Resultado

- Interfaz más limpia y profesional
- Mejora notable en la legibilidad de tablas
- Interacciones más suaves y agradables
- Base preparada para animaciones más avanzadas


## 28/04/2026 — Flujo de entrenamiento y rediseño del historial

Con la ayuda de la IA se continuo haciendo ajustes y mejoras en el diseño e interfaz de la APP

---

### Estados en entrenamiento

Se rediseñó `WorkoutView` con dos estados:

- Preparación: selección de rutina y día
- Entrenamiento activo: registro de series

Se añadió `IsSessionActive` para controlar el cambio entre vistas.

---

### Creación de sesiones

Se implementó la creación de `WorkoutSession` desde un `RoutineDay`, copiando automáticamente sus ejercicios.

Flujo final:
Rutina → Día → Sesión → Entrenamiento

---

### Persistencia y finalización

Las series se guardan directamente al registrarlas.

Se añadió botón de **Finalizar entrenamiento** para cerrar la sesión y mejorar la UX.

---

### Rediseño del historial

Se rehizo `HistoryView` centrado en una sesión seleccionada (por defecto la más reciente), mostrando:

- Ejercicios realizados
- Series registradas

---

### Comparación de rendimiento

Se añadió comparación automática con la sesión anterior del mismo día:

- Mejora de peso
- Mejora de reps
- Igual / inferior rendimiento

---

### Integración con progreso

Se añadió acceso a gráfica desde historial:

- Botón “Ver progreso del ejercicio”
- `ProgressView` ahora recibe `exerciseId`

---

### Simplificación de navegación

Se eliminó la sección “Progreso” del sidebar.

Acceso a gráficas ahora es contextual desde historial.

---

### Converters

Se añadieron:

- `BoolToVisibilityConverter`
- `InverseBoolToVisibilityConverter`

para gestionar los estados de la vista.

---

### Resultado

- Flujo de entrenamiento completo
- Historial centrado en progreso real
- Navegación más limpia y coherente


## 29/04/2026 — Mejoras visuales, navegación y progreso

Se continúan buscando mejoras en la visualización y el manejo de la interfaz para lograr conseguir una app lo más
agradable y cómoda posible para el usuario.

---

### Sidebar y navegación

- Implementación de botón activo mediante borde lateral
- Mejora de la navegación entre vistas con transición fade (entrada/salida)

---

### Animaciones y experiencia de usuario

- Mejora del comportamiento visual en `ListBoxItem` (hover + selección estable)
- Intento de animación en tabs, finalmente se prioriza estabilidad sobre animación

---

### Mejora de ComboBox

- Corrección de colores en texto y fondo
- Ajuste del ancho del desplegable para que coincida con el control
- Integración visual con el tema oscuro de la app

---

### Rediseño de la vista de progreso

- Eliminación del selector de ejercicio (ahora se accede desde historial)
- Conversión a vista contextual (progreso del ejercicio seleccionado)
- Mejora estética del gráfico:
  - Línea verde (color de acento)
  - Eliminación de ruido visual (sin separadores)
  - Puntos visibles en cada sesión
  - Animación de carga

---

### Resultado

- Navegación más fluida y clara
- Interfaz más limpia y coherente
- Gráfica de progreso integrada y visualmente atractiva
- Mejora notable en la experiencia de usuario


## 30/04/2026 — Sistema de notificaciones, modal y pantalla de bienvenida

---

### Sistema de notificaciones (Toast)

- Creación de `NotificationService` global
- Implementación de notificaciones tipo toast en `MainWindow`
- Sustitución de `MessageBox.Show` en avisos no críticos
- Tipos de notificación:
  - Info
  - Success
  - Error
- Animación de entrada/salida (fade)

---

### Modal de confirmación personalizado

- Creación de `ConfirmDialogService`
- Implementación de modal integrado en `MainWindow`
- Sustitución de `MessageBox` en acciones críticas:
  - Finalizar entrenamiento
  - Eliminar rutinas
  - Eliminar ejercicios
- Uso de `async/await` para evitar bloqueo de UI

---

### Pantalla de bienvenida (WelcomeView)

- Creación de `WelcomeView` como pantalla inicial
- Ocultación del sidebar al inicio
- Navegación a `HomeView` mediante botón "Empezar"
- Compuesta de:
  - Card central
  - Mensaje de valor
  - Iconos representativos
  - Fondo decorativo con acento

---

### Animaciones en bienvenida

- Animación de entrada de la card principal:
  - Fade in
  - Desplazamiento vertical (TranslateTransform)
- Mejora de percepción visual y fluidez

---

### Resultado

- Eliminación de ventanas externas (MessageBox)
- UI más consistente y moderna
- Flujo inicial de la app más profesional
- Experiencia de usuario notablemente mejorada


## 02/05/2026 — Branding, sidebar y animaciones UI

---

### Logo e identidad visual

- Creación de logo principal y versión reducida (SP)
- Integración del logo en:
  - WelcomeView (branding principal)
  - Sidebar (parte inferior)
- Ajuste de tamaños y márgenes para correcta visualización
- Uso de imagen con fondo transparente

---

### Rediseño del sidebar

- Reorganización del layout usando Grid:
  - Menú en la parte superior
  - Espacio central flexible
  - Branding y acciones en la parte inferior
- Integración del logo en la esquina inferior izquierda
- Añadido botón de salida minimalista (icono ↪) en esquina inferior derecha
- Eliminación del botón “Salir” tradicional del menú

---

### Comportamiento dinámico del sidebar

- Al colapsar:
  - Se oculta el texto "SteelProgress"
  - Se oculta el botón de salida
  - Se mantiene solo el icono del logo centrado
- Al expandir:
  - Se restauran todos los elementos
- Corrección de errores de visibilidad y alineación

---

### Animación del sidebar

- Sustitución del cambio brusco de ancho por animación suave
- Implementación mediante animación de `Width` en el `Border`
- Uso de `DoubleAnimation` con `QuadraticEase`
- Integración con métodos `ShowSidebar()` y `HideSidebar()`

---

### Animaciones en tablas

- Animación de entrada de filas (`fade-in`)
- Desplazamiento horizontal suave en hover
- Mantenimiento de estilos de selección existentes
- Aplicación global mediante `Style` de `DataGridRow`

---

### Resultado

- UI más fluida y coherente
- Sidebar con comportamiento moderno y profesional
- Branding consistente en toda la app
- Tablas más dinámicas y agradables de usar


## 03/05/2026 — Ajustes finales y control de datos

---

### Control de edición en tablas

- Se ha deshabilitado la edición directa en los `DataGrid`
- Configuración global:
  - `IsReadOnly = true`
  - `CanUserAddRows = false`
  - `CanUserDeleteRows = false`
- Ahora todas las modificaciones se realizan únicamente mediante botones y lógica controlada

---

### Reordenación de ejercicios en rutinas

- Implementada funcionalidad para cambiar el orden de ejercicios dentro de un día:
  - Botones "Subir" y "Bajar"
- Intercambio de propiedad `Order` entre elementos
- Actualización en base de datos mediante repositorio

---

### Corrección de orden tras eliminación

- Solucionado problema de huecos en el orden al eliminar ejercicios
- Implementado método `ReorderDayExercises`:
  - Reasigna valores consecutivos (1, 2, 3...)
- Ajuste en creación de ejercicios:
  - Nuevo `Order = Max + 1` en lugar de `Count`

---

### Protección de navegación durante entreno activo

- Implementado control global con `WorkoutStateService`
- Si hay un entrenamiento activo:
  - Se muestra un modal de confirmación al cambiar de vista
- Evita pérdida accidental de contexto

---

### Configuración de base de datos para entrega

- Verificado que la base de datos se crea automáticamente
- Eliminación de archivos `.db` con datos de prueba
- Uso de migraciones para generar estructura
- Comprobación de persistencia tras reinicio

---

### Preparación para publicación

- Aplicación configurada para iniciarse maximizada
- Validación de funcionamiento en entorno limpio
- Confirmación de que no depende de datos previos

---

### Estado actual

- Aplicación completamente funcional
- Flujo de usuario completo validado
- UI pulida y consistente
- Lista para entrega

---

### Pendiente (para siguiente iteración)

- Permitir cancelar entrenamiento sin guardar datos
- Guardado diferido hasta "Finalizar entrenamiento"


## 04/05/2026 — Gestión de cancelación de entrenamiento

---

### Nuevo flujo de entrenamiento

- Modificado el comportamiento del registro de series durante un entrenamiento:
  - Las series ya no se guardan directamente en base de datos
  - Se almacenan temporalmente en memoria durante la sesión activa

---

### Almacenamiento temporal de series

- Uso de `ObservableCollection<WorkoutSet> CurrentSets` para gestionar las series en curso
- Actualización de la UI para mostrar datos desde memoria en lugar de base de datos
- Eliminación de dependencia directa de la BD durante el entrenamiento

---

### Guardado diferido

- Las series se guardan en base de datos únicamente al finalizar el entrenamiento
- Implementación en `FinishWorkout()`:
  - Recorrido de `CurrentSets`
  - Inserción en base de datos mediante repositorio

---

### Cancelación de entrenamiento

- Añadido botón y lógica para cancelar entrenamiento activo
- Implementación de confirmación mediante modal
- Al cancelar:
  - Se eliminan las series temporales
  - Se elimina la sesión creada en base de datos
  - Se restablece el estado de la UI

---

### Ajustes en lógica de sets

- `AddSet()` ahora añade series solo en memoria
- `DeleteSet()` elimina series de la colección temporal
- Eliminado guardado automático en BD en estas acciones

---

### Integración con estado global

- Uso de `WorkoutStateService` para controlar si hay un entrenamiento activo
- Actualización del estado al:
  - Iniciar entrenamiento
  - Finalizar
  - Cancelar

---

### Resultado

- Flujo de entrenamiento más realista y controlado
- El usuario puede decidir si guardar o descartar la sesión
- Mejora significativa en experiencia de usuario y control de datos
