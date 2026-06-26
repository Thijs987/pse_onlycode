Copy the template into a new file named .env in the root folder of your project. You will need to fill in the missing fields before running the server:
+ Your database connection string.
+ Your SMTP email server credentials (for sending registration emails).
+ A long, secure, random string (atleast 32 characters) for your JWT Key.

Note: Never commit your completed .env file to version control.

```# Environment configuration for PSE-Green-Code
# Keep this file secure and do NOT commit it to version control.

ConnectionStrings__Default=

# Email settings
EmailSettings__Host=
EmailSettings__Port=
EmailSettings__Username=
EmailSettings__Password=
EmailSettings__EnableSsl=true
EmailSettings__FromEmail=
EMAIL_SMTP_USERNAME=
EMAIL_SMTP_PASSWORD=

# Application settings
AppSettings__BaseUrl=https://localhost:6969
AppSettings__EnforceAuth=true
AppSettings__AllowHttp=false
AppSettings__AllowedOrigins__0=https://localhost:6969
AppSettings__RateLimit__Enabled=true
AppSettings__RateLimit__Requests=60
AppSettings__RateLimit__WindowSeconds=60

# JWT
Jwt__Key=''
Jwt__Issuer=PSE-Green
Jwt__Audience=PSE-Green-Clients
