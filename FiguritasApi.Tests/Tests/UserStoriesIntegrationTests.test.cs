using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FiguritasApi.Tests;

/// <summary>
/// Pruebas de integración para las User Stories de la Entrega 1.
/// Utiliza WebApplicationFactory para levantar la API en memoria y testear el diseño REST.
/// </summary>
public class UserStoriesIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public UserStoriesIntegrationTests(WebApplicationFactory<Program> factory)
    {
        // Se inicializa el cliente HTTP que apuntará a la aplicación en memoria
        _client = factory.CreateClient();
        var usuario = new
        {
            id = 0,
            nombreUsuario = "string",
            figuritasRepetidas = new[]
            {
                new
                {
                    id = 0,
                    figurita = new
                    {
                        id = 0,
                        numero = 0,
                        seleccion = "Argentina",
                        equipo = "Boca",
                        categoria = "Prueba"
                    },
                    usuarioID = 0,
                    puedeIntercambiarse = true,
                    activo = true,
                    cantidad = 0
                }
            },
            figuritasFaltantes = new[]
            {
                new
                {
                    id = 0,
                    numero = 0,
                    seleccion = "Argentina",
                    equipo = "Boca",
                    categoria = "Prueba"
                }
            },
            reputacion = 0
        };
        _client.PostAsJsonAsync($"/usuarios", usuario);
    }

    [Fact]
    public async Task US01_PublicarFiguritaRepetida_DeberiaRetornarCreated()
    {
        // Arrange
        var usuarioId = 0;
        var nuevaRepetida = new
        {
            figurita = new
            {
                id = 0,
                numero = 34,
                seleccion = 0,
                equipo = 0,
                categoria = 0
            },
            puedeIntercambiarse = true,
            activo = true
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/usuarios/{usuarioId}/Repetidas", nuevaRepetida);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        //Assert.NotNull(response.Headers.Location);
    }

    [Fact]
    public async Task US02_RegistrarFiguritaFaltante_DeberiaRetornarOk()
    {
        // Arrange
        var usuarioId = 0;
        var figuritaFaltante = new
        {
            figurita = new
            {
                id = 0,
                numero = 23,
                seleccion = "Argentina",
                equipo = "Boca",
                categoria = "Prueba"
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync($"usuarios/{usuarioId}/faltantes", figuritaFaltante);

        // Assert
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task US03_BuscarFiguritasDisponibles_DeberiaRetornarListaFiltrada()
    {
        // Arrange
        await _client.PostAsync($"/FiguritasRepetidas", JsonContent.Create(new
        {
            id = 0,
            figurita = new
            {
                id = 0,
                numero = 0,
                seleccion = "Argentina",
                equipo = "Boca",
                categoria = "Prueba"
            },
            usuarioID = 0,
            puedeIntercambiarse = true,
            activo = true,
            cantidad = 0
        }));

        var numeroABuscar = 0;

        var response = await _client.GetAsync($"/FiguritasRepetidas?numero={numeroABuscar}");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Console.WriteLine(content);
        Assert.Contains("Prueba", content); // Asumiendo que el mock en memoria devuelve este dato
    }

    //TODO implementar test para userStory4?

    [Fact]
    public async Task US05_HacerPropuestaIntercambio_DeberiaRetornarCreated()
    {
        // Arrange
        var usuarioProponenteId = 0;
        var usuarioReceptorId = 0;
        var propuesta = new
        {
            usuarioProponenteID = 0,
            figuritasOfrecidas = new[]
            {
                new
                {
                    id = 0,
                    figurita = new
                    {
                        id = 0,
                        numero = 0,
                        seleccion = "Argentina",
                        equipo = "Boca",
                        categoria = "Prueba"
                    },
                    usuarioID = 0,
                    puedeIntercambiarse = true,
                    activo = true,
                    cantidad = 0
                }
            },
            figuritasARecibir = new[]
            {
                new
                {
                    id = 0,
                    numero = 0,
                    seleccion = "Argentina",
                    equipo = "Boca",
                    categoria = "Prueba"
                }
            }
        };

        var response = await _client.PostAsJsonAsync($"usuarios/{usuarioReceptorId}/intercambios", propuesta);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task US06_PublicarFiguritaEnSubasta_DeberiaRetornarCreated()
    {
        // Arrange
        var figuritaRepetidaId = 150;
        var reglasSubasta = new
        {
            DuracionHoras = 48,
            CantidadMinimaFiguritasRequeridas = 2
        };

        // Act - Mapeando a un recurso anidado en FiguritasRepetidasController
        var response = await _client.PostAsJsonAsync($"/api/figuritas-repetidas/{figuritaRepetidaId}/subasta", reglasSubasta);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task US07_OfertarEnSubastaActiva_DeberiaRetornarOk()
    {
        // Arrange
        var subastaId = 10;
        var oferta = new
        {
            UsuarioOfertanteId = 2,
            FiguritasOfrecidasIds = new[] { 300, 301 }
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/subastas/{subastaId}/ofertas", oferta);

        // Assert
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task US08_VerTableroDeUsuario_DeberiaRetornarEstadoCompleto()
    {
        // Arrange
        var usuarioId = 1;

        // Act
        var response = await _client.GetAsync($"/api/usuarios/{usuarioId}/dashboard");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("publicadas", content);
        Assert.Contains("propuestas", content);
        Assert.Contains("subastasActivas", content);
    }

    [Fact]
    public async Task US09_AceptarPropuestaRecibida_DeberiaActualizarEstadoYRetornarOk()
    {
        // Arrange
        var usuarioId = 2; // El que recibe
        var propuestaId = 5;
        var cambioDeEstado = new { Estado = "Aceptada" }; // Podría mapear al enum EstadoPropuestaIntercambio

        // Act
        var response = await _client.PutAsJsonAsync($"/api/usuarios/{usuarioId}/propuestas/{propuestaId}/estado", cambioDeEstado);

        // Assert
        Assert.True(response.IsSuccessStatusCode);
    }
}