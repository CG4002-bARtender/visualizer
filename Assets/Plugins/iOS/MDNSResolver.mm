#include <netdb.h>
#include <arpa/inet.h>
#include <string.h>

extern "C" {
    // Resolves a hostname (including .local mDNS) to an IPv4 string.
    // Returns pointer to a static buffer, or NULL on failure.
    const char* ResolveMDNS(const char* hostname) {
        struct addrinfo hints, *res = NULL;
        memset(&hints, 0, sizeof(hints));
        hints.ai_family = AF_INET;
        hints.ai_socktype = SOCK_STREAM;

        if (getaddrinfo(hostname, NULL, &hints, &res) != 0 || res == NULL)
            return NULL;

        static char result[INET_ADDRSTRLEN];
        struct sockaddr_in* addr = (struct sockaddr_in*)res->ai_addr;
        inet_ntop(AF_INET, &addr->sin_addr, result, sizeof(result));
        freeaddrinfo(res);
        return result;
    }
}
