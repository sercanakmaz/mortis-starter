namespace Boilerplate.Shared.Application;

public interface ISecretProtector
{
    string Protect(string plaintext);
    string Unprotect(string envelope);
}
