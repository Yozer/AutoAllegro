/// <binding Clean='clean' />
"use strict";

var gulp = require("gulp"),
    rimraf = require("rimraf"),
    concat = require("gulp-concat"),
    cssmin = require("gulp-cssmin"),
    uglify = require("gulp-uglify"),
    sass = require('gulp-sass'),
    addsrc = require('gulp-add-src');

var paths = {
    webroot: "./wwwroot/"
};

paths.js = paths.webroot + "js/**/*.js";
paths.minJs = paths.webroot + "js/**/*.min.js";
paths.css = paths.webroot + "css/main*.css";
paths.minCss = paths.webroot + "css/**/*.min.css";
paths.sass = paths.webroot + "css/**/*.scss";
paths.concatJsDest = paths.webroot + "js/main.min.js";
paths.concatCssDest = paths.webroot + "css/main.min.css";
paths.scssDest = paths.webroot + "css/";

gulp.task("clean:js", function (cb) {
    rimraf(paths.concatJsDest, cb);
});

gulp.task("clean:css", function (cb) {
    rimraf(paths.css, cb);
});

gulp.task("clean", ["clean:js", "clean:css"]);

gulp.task('compile:sass', function () {
    gulp.src(paths.sass)
        .pipe(sass())
        .pipe(gulp.dest(paths.scssDest));
});

gulp.task("compile", ["compile:sass"]);

gulp.task("min:js", function () {
    return gulp.src([paths.js, "!" + paths.minJs], { base: "." })
        .pipe(addsrc(paths.webroot + "lib/jquery-validation/dist/jquery.validate.js"))
        .pipe(addsrc(paths.webroot + "lib/jquery-validation-unobtrusive/jquery-validation-unobtrusive.js"))
        .pipe(addsrc(paths.webroot + "lib/bootstrap/dist/js/bootstrap.min.js"))
        .pipe(concat(paths.concatJsDest))
        .pipe(uglify())
        .pipe(gulp.dest("."));
});

gulp.task("min:css", function () {
    return gulp.src([paths.css, "!" + paths.minCss])
        .pipe(addsrc(paths.webroot + "css/bootstrap-custom.css"))
        .pipe(concat(paths.concatCssDest))
        .pipe(cssmin())
        .pipe(gulp.dest("."));
});


gulp.task("min", ["min:js", "min:css"]);
