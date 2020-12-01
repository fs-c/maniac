add_executable(maniac-app app.cpp)

target_compile_features(maniac-app PRIVATE cxx_std_17)

target_link_libraries(maniac-app PRIVATE maniac)

set_target_properties(maniac-app PROPERTIES OUTPUT_NAME "maniac")
