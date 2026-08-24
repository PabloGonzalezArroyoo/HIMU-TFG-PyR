# Herramienta de Interacción Multipantalla para Unity (HIMU)
*HIMU* es una herramienta para Unity 6 que permite usar el smartphone como mando en juegos que se ejecuten en una pantalla principal (p.e. un ordenador), además de la retransmisión de la partida a un navegador. Cuenta con una app universal que se adapta a cualquier juego que haga el desarrollador con esta herramienta.

## Autores
 
- **Pablo González Arroyo**
- **Rafael Argandoña Blácido**

**Dirección del proyecto:** Alejandro Romero Hernández
 
Trabajo de Fin de Grado - Grado en Desarrollo de Videojuegos

Facultad de Informática, Universidad Complutense de Madrid

Curso 2025/2026

## Funcionalidades
 
*HIMU* cuenta con las siguientes funcionalidades:
 
- **Conexión de jugadores** desde el móvil hacia el anfitrión de forma automática y sin configuración manual, gracias al uso de la aplicación universal.
- **Envío de entrada** desde cada dispositivo hasta el juego, con la latencia baja que exige el uso en tiempo real.
- **Envío de vídeo**, aumentando la potencialidad del smartphone como segunda pantalla y para la retransmisión a navegador.
- **Integración con la lógica del juego**, exponiendo el intercambio de información de cada jugador de una forma cómoda de consumir desde Unity.

## Contenido del repositorio

Este repositorio cuenta con dos carpetas:

- *HIMU_Client* - Es donde está alojada la lógica de la aplicación necesaria para jugar a los juegos desarrollados con esta herramienta.
- *HIMU_Host* - Es donde está alojada tanto la lógica de la herramienta como los dos juegos de prueba (shooter y racer).