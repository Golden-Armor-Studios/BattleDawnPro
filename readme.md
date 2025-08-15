## Unity Framework - AWS Login in the editor

Login using the plugin into a AWS Cognito user db.
The ID token from login is provided into a editorPref that you can add to your AWS Lambda game microservices url in the Authorization header.

Authorization: Bearer EditorPrefs.GetString("AWS_IDTOKEN");