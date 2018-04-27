using System;
using System.Threading.Tasks;

namespace ThunderED.Zkb
{
    public interface IRequestHandler {
        /// <summary>
        ///     Gets or sets the serializer used to deserialize data
        /// </summary>
        ISerializer Serializer { get; set; }

        /// <summary>
        ///     Performs a request and returns the deserialized response content
        /// </summary>
        /// <typeparam name="T">Type to deserialize to</typeparam>
        /// <param name="uri">URI to request</param>
        /// <returns>Deserialized response</returns>
        Task<T> RequestAsync<T>(Uri uri);
    }

    public interface ISerializer {
        /// <summary>
        ///     Deserializes data.
        /// </summary>
        /// <typeparam name="T">Type to deserialize to.</typeparam>
        /// <param name="data">String of data to deserialize.</param>
        /// <returns></returns>
        T Deserialize<T>(string data);

        /// <summary>
        ///     Serializes the specified entity.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="entity">The entity.</param>
        /// <returns>System.String.</returns>
        string Serialize<T>(T entity);
    }
}
