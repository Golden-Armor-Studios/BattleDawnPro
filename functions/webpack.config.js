const path = require("path");

module.exports = {
	mode: "production",
	entry: "./src/index.js",
	target: "node18",
	output: {
		filename: "index.js",
		path: path.resolve(__dirname, "dist"),
		libraryTarget: "commonjs2"
	},
	resolve: {
		extensions: [".js"]
	},
	module: {
		rules: [
			{
				test: /\.js$/,
				exclude: /node_modules/,
				use: {
					loader: "babel-loader"
				}
			}
		]
	},
	externalsPresets: { node: true },
	externals: {
		"firebase-functions": "commonjs firebase-functions",
		"firebase-admin": "commonjs firebase-admin",
		"@google-cloud/tasks": "commonjs @google-cloud/tasks"
	},
	optimization: {
		minimize: false
	},
	devtool: "source-map"
};
